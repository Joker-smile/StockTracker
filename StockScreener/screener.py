# -*- coding: utf-8 -*-
import requests
import pandas as pd
import datetime
from tqdm import tqdm
import time
import warnings
import json
import os
import sys

warnings.filterwarnings('ignore')

def get_base_stocks():
    """
    步骤 1: 获取沪深A股实时基础数据，并进行基本面+盘子体检
    """
    print("正在获取A股全市场实时数据(东方财富接口)...")
    try:
        url = "http://82.push2.eastmoney.com/api/qt/clist/get"
        # f12 代码, f14 名称, f2 最新价, f9 动态市盈率
        # f6 成交额, f8 换手率, f20 总市值
        params = {
            "pn": "1",
            "pz": "10000",
            "po": "1",
            "np": "1",
            "ut": "bd1d9ddb04089700cf9c27f6f7426281",
            "fltt": "2",
            "invt": "2",
            "fid": "f3",
            "fs": "m:0 t:6,m:0 t:80,m:1 t:2,m:1 t:23,m:0 t:81 s:2048",
            "fields": "f12,f14,f2,f8,f9,f20"
        }
        res = requests.get(url, params=params, timeout=10)
        data = res.json()
        
        if not data.get("data") or not data["data"].get("diff"):
            print("未能获取到数据列表。")
            return pd.DataFrame()
            
        items = data["data"]["diff"]
        
        df_list = []
        for item in items:
            df_list.append({
                "代码": item.get("f12", ""),
                "名称": item.get("f14", ""),
                "最新价": item.get("f2", 0),
                "换手率": item.get("f8", 0),
                "动态市盈率": item.get("f9", 0),
                "总市值": item.get("f20", 0)
            })
            
        df_spot = pd.DataFrame(df_list)
        
        # 数据清洗: 过滤空值和非正常价格
        df_spot = df_spot[df_spot['最新价'].notna() & (df_spot['最新价'] != '-')]
        df_spot = df_spot[df_spot['换手率'].notna() & (df_spot['换手率'] != '-')]
        df_spot = df_spot[df_spot['动态市盈率'].notna() & (df_spot['动态市盈率'] != '-')]
        df_spot = df_spot[df_spot['总市值'].notna() & (df_spot['总市值'] != '-')]
        
        df_spot['最新价'] = pd.to_numeric(df_spot['最新价'], errors='coerce')
        df_spot['换手率'] = pd.to_numeric(df_spot['换手率'], errors='coerce')
        df_spot['动态市盈率'] = pd.to_numeric(df_spot['动态市盈率'], errors='coerce')
        df_spot['总市值'] = pd.to_numeric(df_spot['总市值'], errors='coerce')
        
        # 过滤ST和退市
        df_spot = df_spot[~df_spot['名称'].str.contains('ST|退', na=False)]
        
        print(f"全市场正常交易股票数量: {len(df_spot)}")
        
        # 核心防线 1: 盈利初筛 (PE > 0 且 PE < 40)
        df_spot = df_spot[(df_spot['动态市盈率'] > 0) & (df_spot['动态市盈率'] < 40)]
        
        # 核心防线 2: 市值与活跃度排雷
        # -- 剔除市值 > 500亿 的大盘权重 (涨不动) 和市值 < 20亿 的超级微盘 (容易退市) 
        # -- 东方财富f20接口返回的是实际数值。500亿 = 50,000,000,000
        df_spot = df_spot[(df_spot['总市值'] > 2000000000) & (df_spot['总市值'] < 50000000000)]
        
        # -- 换手率过滤: 当日必须有交投 (换手率 > 1.2%)
        df_spot = df_spot[df_spot['换手率'] > 1.2]

        print(f"经过【基础池(PE<40, 市值50-500亿, 有成交换手)】过滤后剩余: {len(df_spot)} 只")
        
        return df_spot
        
    except Exception as e:
        print(f"获取基础数据失败: {e}")
        return pd.DataFrame()

def check_technical_and_peg(symbol, name, current_price, pe, market_cap):
    """
    步骤 2: 技术面检查(年线200日, 20日支撑位, 放量暴跌, 换手率蓄势)
    """
    secid = "1." + symbol if symbol.startswith('6') else "0." + symbol
    url = "http://push2his.eastmoney.com/api/qt/stock/kline/get"
    params = {
        "secid": secid,
        "ut": "7eea3edcaed734bea9cbbc2440b282fb",
        "fields1": "f1,f2,f3,f4,f5,f6",
        "fields2": "f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61",
        "klt": "101", 
        "fqt": "1",
        "end": "20500101",
        "lmt": "250", 
    }
    
    try:
        res = requests.get(url, params=params, timeout=5)
        data = res.json()
        if not data.get("data") or not data["data"].get("klines"):
            return None
            
        klines = data["data"]["klines"]
        if len(klines) < 200:
            return None # 上市不满1年
            
        closes = []
        vols = []
        pcts = []
        turnovers = []
        for k in klines:
            items = k.split(",")
            closes.append(float(items[2]))
            vols.append(float(items[5]))
            pcts.append(float(items[8]))
            try:
                turnovers.append(float(items[10])) # 换手率
            except:
                turnovers.append(0.0)
            
        closes = pd.Series(closes)
        vols = pd.Series(vols)
        pcts = pd.Series(pcts)
        turnovers = pd.Series(turnovers)
        
        # 1. 计算均线
        ma200 = closes.rolling(window=200).mean().iloc[-1]
        ma20 = closes.rolling(window=20).mean().iloc[-1]
        
        # 检查 A: 长线安全垫 (站在年线之上)
        if current_price < ma200:
            return None
            
        # 检查 B: 短线爆发力前奏 (站在 20 日线之上，且偏离率不能太恐怖（追涨风险）)
        # 偏离太远（比如比 MA20 高了 20%以上）说明短期已经炒高了
        if current_price < ma20 or (current_price / ma20 - 1) > 0.15:
            return None

        # 检查 C: 异动过滤 (最近 10 天不能有放量阴跌/暴跌)
        recent_10_pct = pcts.tail(10)
        recent_10_vol = vols.tail(10)
        v_ma20_all = vols.rolling(window=20).mean()
        v_ma20_recent = v_ma20_all.iloc[-10:]
        
        is_crash = (recent_10_pct < -5.0) & (recent_10_vol > v_ma20_recent.values * 1.5)
        if is_crash.any():
            return None 

        # 检查 D: 游资/庄家活跃度 (换手率脉冲)
        # 条件：最近 5 天某一日换手率 > 5%，或者最近 5 天平均换手率 > 3%
        recent_5_turnover = turnovers.tail(5)
        if not (recent_5_turnover.max() > 5.0 or recent_5_turnover.mean() > 3.0):
            return None

        return {
            '代码': symbol,
            '名称': name,
            '现价': f"{current_price:.2f}",
            'MA20价': f"{ma20:.2f}",
            '年线价': f"{ma200:.2f}",
            'PE': f"{pe:.1f}",
            '市值(亿)': round(market_cap / 100000000, 1)
        }
    except Exception:
        return None

def get_stock_concepts(symbol):
    """
    获取单只股票的所属板块/概念
    """
    secucode = f"{symbol}.SZ" if symbol.startswith('0') or symbol.startswith('3') else f"{symbol}.SH"
    url = "https://datacenter-web.eastmoney.com/api/data/v1/get"
    params = {
        "reportName": "RPT_F10_CORETHEME_BOARDTYPE",
        "columns": "BOARD_NAME",
        "filter": f'(SECUCODE="{secucode}")',
        "pageNumber": 1,
        "pageSize": 50,
    }
    
    headers = {
        "User-Agent": "Mozilla/5.0",
        "Referer": "http://data.eastmoney.com/"
    }
    try:
        res = requests.get(url, params=params, headers=headers, timeout=3)
        data = res.json()
        if data and data.get("result") and data["result"].get("data"):
            tags = [item.get("BOARD_NAME") for item in data["result"]["data"]]
            
            # 过滤掉一些太宽泛或者没用的板块词缀以保持版面整洁
            blacklist = ["融资融券", "深股通", "沪股通", "标普走势", "MSCI中国", "富时罗素", " HS300", "深证100"]
            clean_tags = []
            for t in tags:
                if not any(b in t for b in blacklist):
                    clean_tags.append(t)
            
            # 为了展示干净，最多取前 4 个核心概念 (东方财富按热度大概率排在前面)
            return ",".join(clean_tags[:4])
        return "无"
    except Exception:
        return ""

def run_screener():
    print("="*60)
    print("大A 相亲选股器 (高弹性尊享版)")
    print("1. [家底] 连续盈利 且 PE < 40")
    print("2. [体型] 剔除超大盘, 锁定 50 - 500 亿的中盘成长股")
    print("3. [趋势] 股价 > 200日(年线) 且 > 20日线 (且偏离率在安全范围)")
    print("4. [脾气] 无放量暴跌，且近 5 日有脉冲性游资换手 (活跃标的)")
    print("5. [灵魂] 提取最前排的所属核心概念与行业")
    print("="*60)
    
    # 获取基础池
    df_base = get_base_stocks()
    if df_base.empty:
        print("未能获取到基础池数据，请检查网络。")
        return

    # 这里不再只切前 300，因为前面的市值+换手率过滤已经把5000只浓缩到了几百只
    # 我们按换手率从大到小排序，寻找最活跃的“相亲对象”
    df_base = df_base.sort_values(by='换手率', ascending=False)
    
    # 为了保护 API 防封，限制最多检查 300 只（这通常足够覆盖活跃度最好的目标）
    process_list = df_base.head(300)
    print(f"\n基础池提取了 {len(process_list)} 只符合[体型+家底]的活跃标的。")
    print(f"正在逐个剖析其[趋势K线+游资脉冲]特征...")
    
    passed_stocks = []
    
    for index, row in tqdm(process_list.iterrows(), total=len(process_list), desc="K线深度排队体检"):
        symbol = str(row['代码']).zfill(6)
        name = row['名称']
        price = row['最新价']
        pe = row['动态市盈率']
        mc = row['总市值']
        
        tech_result = check_technical_and_peg(symbol, name, price, pe, mc)
        if tech_result:
            # 增加获取概念板块
            concepts = get_stock_concepts(symbol)
            tech_result['概念题材'] = concepts
            
            passed_stocks.append(tech_result)
            time.sleep(0.02) # 友好的请求间隔
            
    print("\n\n" + "="*80)
    if not passed_stocks:
        print("全军覆没！今天没有同时具备极品身段和活跃表现的对象。")
    else:
        print(f"万里挑一！在大A找到了 {len(passed_stocks)} 位值得重仓的顶尖相亲对象！")
        
        result_df = pd.DataFrame(passed_stocks)
        result_df = result_df[['代码', '名称', '现价', 'MA20价', '年线价', 'PE', '市值(亿)', '概念题材']]
        # 按市值从小到大排序展示，盘子越小越能妖
        result_df = result_df.sort_values(by='市值(亿)', ascending=True)
        print(result_df.to_string(index=False))
        
        # 写入 StockTracker 的 stocks.txt
        current_dir = os.path.dirname(os.path.abspath(__file__))
        base_dir = os.path.dirname(current_dir)
        
        # 默认需要同步的三个publish目标的stocks.txt
        target_paths = [
            os.path.join(base_dir, "publish", "win-x64", "stocks.txt"),
            os.path.join(base_dir, "publish", "osx-x64", "stocks.txt"),
            os.path.join(base_dir, "publish", "osx-arm64", "stocks.txt")
        ]
        
        # 如果从C#传入了具体的运行时刻_configFile，也加进去
        if len(sys.argv) > 1:
            caller_path = sys.argv[1]
            if caller_path not in target_paths:
                target_paths.append(caller_path)
                
        total_added = 0
        for txt_path in target_paths:
            # 确保目录存在
            os.makedirs(os.path.dirname(txt_path), exist_ok=True)
            
            existing_codes = set()
            try:
                with open(txt_path, 'r', encoding='utf-8') as f:
                    for line in f:
                        code = line.strip()
                        if code:
                            existing_codes.add(code)
            except FileNotFoundError:
                pass # 如果文件不存在，我们就当它是空的去新建
                
            new_codes_added = 0
            with open(txt_path, 'a', encoding='utf-8') as f:
                for index, row in result_df.iterrows():
                    code = str(row['代码']).zfill(6)
                    if code not in existing_codes:
                        f.write(code + '\n')
                        existing_codes.add(code)
                        new_codes_added += 1
            if new_codes_added > 0:
                dir_name = os.path.basename(os.path.dirname(txt_path))
                print(f"[系统提示] 成功向 {dir_name}/stocks.txt 追加了 {new_codes_added} 只新标的。")
            total_added += new_codes_added
            
        if total_added == 0:
            print(f"\n[系统提示] 本次选出的股票均已在所有追踪列表中，无新增。")
        else:
            print(f"\n[系统提示] 同步完毕，所有版本的热重载列表均已更新！")
        
        print("\n* 投资提示: 建议在此名单中，肉眼挑选有【核心科技/热门算力/政策红利】概念的公司。")
        
if __name__ == "__main__":
    run_screener()
