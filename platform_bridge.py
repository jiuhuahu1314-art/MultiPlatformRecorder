#!/usr/bin/env python3
"""Bridge between BililiveRecorder WPF and streamget library.
Usage: python platform_bridge.py <room_url> [cookie_string]
Output: JSON with live status and stream URL
"""
import asyncio
import json
import importlib
import sys
import re
import io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

PLATFORM_NAMES = {
    'DouyinLiveStream': '抖音', 'TikTokLiveStream': 'TikTok',
    'KwaiLiveStream': '快手', 'HuyaLiveStream': '虎牙',
    'DouyuLiveStream': '斗鱼', 'YYLiveStream': 'YY',
    'BilibiliLiveStream': 'B站', 'TwitchLiveStream': 'Twitch',
    'YoutubeLiveStream': 'YouTube', 'KugouLiveStream': '酷狗',
    'HuajiaoLiveStream': '花椒', 'AcfunLiveStream': 'Acfun',
    'WeiboLiveStream': '微博', 'BaiduLiveStream': '百度',
    'InkeLiveStream': '映客', 'ZhihuLiveStream': '知乎',
    'YiqiLiveStream': '17Live', 'LangLiveStream': '浪Live',
    'ShowRoomLiveStream': 'ShowRoom', 'BigoLiveStream': 'Bigo',
    'BluedLiveStream': 'Blued', 'LiveMeLiveStream': 'LiveMe',
    'SoopLiveStream': 'SOOP', 'PandaLiveStream': 'PandaTV',
    'NeteaseLiveStream': '网易CC', 'LookLiveStream': 'Look',
    'MaoerLiveStream': '猫耳FM', 'MiguLiveStream': '咪咕',
    'TwitCastingLiveStream': 'TwitCasting', 'PopkonTVLiveStream': 'PopkonTV',
    'FlexTVLiveStream': 'FlexTV', 'SixRoomLiveStream': '六间房',
    'PicartoLiveStream': 'Picarto', 'ShopeeLiveStream': 'Shopee',
    'TaobaoLiveStream': '淘宝', 'JDLiveStream': '京东',
    'FaceitLiveStream': 'Faceit', 'XindongreboLiveStream': '千度热播',
    'HaixiuLiveStream': '嗨秀', 'LaixiuLiveStream': '来秀',
    'LehaiLiveStream': '乐嗨', 'LianJieLiveStream': '连接',
    'ChzzkLiveStream': 'CHZZK', 'RedNoteLiveStream': '小红书',
}

PLATFORM_MAP = {
    'douyin.com': 'DouyinLiveStream',
    'tiktok.com': 'TikTokLiveStream',
    'kuaishou.com': 'KwaiLiveStream',
    'huya.com': 'HuyaLiveStream',
    'douyu.com': 'DouyuLiveStream',
    'yy.com': 'YYLiveStream',
    'bilibili.com': 'BilibiliLiveStream',
    'bilibili.tv': 'BilibiliLiveStream',
    'twitch.tv': 'TwitchLiveStream',
    'youtube.com': 'YoutubeLiveStream',
    'youtu.be': 'YoutubeLiveStream',
    'kugou.com': 'KugouLiveStream',
    'huajiao.com': 'HuajiaoLiveStream',
    'acfun.cn': 'AcfunLiveStream',
    'weibo.com': 'WeiboLiveStream',
    'baidu.com': 'BaiduLiveStream',
    'inke.cn': 'InkeLiveStream',
    'zhihu.com': 'ZhihuLiveStream',
    '17.live': 'YiqiLiveStream',
    'lang.live': 'LangLiveStream',
    'showroom-live.com': 'ShowRoomLiveStream',
    'bigo.tv': 'BigoLiveStream',
    'blued.cn': 'BluedLiveStream',
    'liveme.com': 'LiveMeLiveStream',
    'sooplive.co.kr': 'SoopLiveStream',
    'pandalive.co.kr': 'PandaLiveStream',
    'cc.163.com': 'NeteaseLiveStream',
    'look.163.com': 'LookLiveStream',
    'fm.missevan.com': 'MaoerLiveStream',
    'miguvideo.com': 'MiguLiveStream',
    'twitcasting.tv': 'TwitCastingLiveStream',
    'popkontv.com': 'PopkonTVLiveStream',
    'flextv.co.kr': 'FlexTVLiveStream',
    '6.cn': 'SixRoomLiveStream',
    'picarto.tv': 'PicartoLiveStream',
    'shopee': 'ShopeeLiveStream',
    'taobao.com': 'TaobaoLiveStream',
    'jd.com': 'JDLiveStream',
    'faceit.com': 'FaceitLiveStream',
    'xinpianchang.com': 'XindongreboLiveStream',
    'haixiutv.com': 'HaixiuLiveStream',
    'imkktv.com': 'LaixiuLiveStream',
    'lehaitv.com': 'LehaiLiveStream',
    'lailianjie.com': 'LianJieLiveStream',
    'chzzk.naver.com': 'ChzzkLiveStream',
    'xiaohongshu.com': 'RedNoteLiveStream',
    'xhslink': 'RedNoteLiveStream',
}


def detect_platform(url: str) -> str | None:
    for keyword, class_name in PLATFORM_MAP.items():
        if keyword in url.lower():
            return class_name
    return None


def extract_basic_info(data: dict) -> tuple:
    """Extract anchor_name and title from any data dict."""
    anchor = ''
    title = ''
    if isinstance(data, dict):
        for key in ['anchor_name', 'nickname', 'nick', 'name', 'displayName', 'userName']:
            val = data.get(key)
            if val and isinstance(val, str):
                anchor = val
                break
        for key in ['title', 'room_name', 'roomName']:
            val = data.get(key)
            if val and isinstance(val, str):
                title = val
                break
        # Nested lookups
        if not anchor:
            user = data.get('user', {})
            if isinstance(user, dict):
                anchor = user.get('nickname', '') or user.get('name', '') or ''
            room = data.get('room', {})
            if isinstance(room, dict):
                if not anchor:
                    anchor = room.get('nickname', '') or ''
                if not title:
                    title = room.get('room_name', '') or room.get('title', '') or ''
            live_info = data.get('liveInfo', {}) or data.get('liveroom', {}).get('liveInfo', {})
            if isinstance(live_info, dict):
                if not anchor:
                    anchor = live_info.get('nick', '') or live_info.get('user', {}).get('name', '') or ''
                if not title:
                    title = live_info.get('title', '') or ''
    return anchor, title


async def get_stream(url: str, cookie: str = "") -> dict:
    class_name = detect_platform(url)
    if not class_name:
        return {"error": f"Unsupported platform URL: {url}"}

    try:
        module = importlib.import_module("streamget")
        LiveClass = getattr(module, class_name)
    except (ImportError, AttributeError) as e:
        return {"error": f"Failed to load {class_name}: {e}"}

    try:
        live = LiveClass(proxy_addr=None, cookies=cookie if cookie else None)
        raw_data = await live.fetch_web_stream_data(url, process_data=True)

        anchor, title = extract_basic_info(raw_data)
        platform = PLATFORM_NAMES.get(class_name, class_name.replace('LiveStream', ''))
        result = {"anchor_name": anchor, "title": title, "platform": platform}

        # If data already has is_live from streamget, trust it
        if isinstance(raw_data, dict) and raw_data.get('is_live'):
            result["is_live"] = True
            try:
                stream = await live.fetch_stream_url(raw_data)
                s = json.loads(stream.to_json())
                result["stream_url"] = s.get('record_url') or s.get('flv_url') or s.get('m3u8_url') or ''
                result["flv_url"] = s.get('flv_url', '')
                result["m3u8_url"] = s.get('m3u8_url', '')
            except Exception as e:
                result["error"] = f"fetch_stream_url failed: {e}"
            return result

        # Try to get stream URL regardless - streamget's fetch_stream_url knows
        # how to extract from the raw response per-platform
        try:
            stream = await live.fetch_stream_url(raw_data)
            s = json.loads(stream.to_json())
            is_live = s.get('is_live', False) or bool(s.get('record_url') or s.get('flv_url') or s.get('m3u8_url'))
            result["is_live"] = is_live
            if is_live:
                result["stream_url"] = s.get('record_url') or s.get('flv_url') or s.get('m3u8_url') or ''
                result["flv_url"] = s.get('flv_url', '')
                result["m3u8_url"] = s.get('m3u8_url', '')
                if not anchor:
                    result["anchor_name"] = s.get('anchor_name', '') or anchor
                if not title:
                    result["title"] = s.get('title', '') or title
            elif not anchor:
                result["is_live"] = False
            return result
        except Exception as e:
            result["is_live"] = False
            if anchor:
                return result
            return {"error": f"fetch_stream_url failed: {e}", "url": url, "class": class_name}

    except Exception as e:
        return {"error": f"Stream get failed: {e}", "url": url}


if __name__ == '__main__':
    if len(sys.argv) < 2:
        print(json.dumps({"error": "Usage: python platform_bridge.py <url> [cookie]"}))
        sys.exit(1)

    url = sys.argv[1]
    cookie = sys.argv[2] if len(sys.argv) > 2 else ""
    result = asyncio.run(get_stream(url, cookie))
    print(json.dumps(result, ensure_ascii=False))
