/** 媒体地址的纯展示投影；原URI继续用于授权编辑和播放，脱敏值不可回写。 */
const sensitiveKeys = new Set(['authorization', 'cookie', 'password', 'current_password', 'new_password',
  'csrf', 'csrftoken', 'session', 'sessionid', 'token', 'access_token', 'refresh_token', 'claim_token',
  'secret', 'api_key', 'passphrase', 'srt-passphrase']);

/** :param value: 原媒体URI或本地路径。:returns: 隐藏userinfo及已知敏感查询的显示值，不改变调用者原值。 */
export function displayMediaAddress(value: string): string {
  try {
    const address = new URL(value);
    if (!['http:', 'https:', 'rtsp:', 'rtmp:', 'srt:', 'udp:', 'rtp:'].includes(address.protocol)) return value;
    let changed = false;
    if (address.username || address.password) {
      address.username = '***'; address.password = '***'; changed = true;
    }
    for (const key of [...address.searchParams.keys()]) {
      const normalized = key.toLowerCase();
      if (sensitiveKeys.has(normalized) || /_(password|token|secret)$/.test(normalized)) {
        address.searchParams.set(key, '***'); changed = true;
      }
    }
    return changed ? address.href : value;
  } catch {
    return /^[a-z][\w+.-]*:\/\//i.test(value) && value.includes('@') ? '地址无效（凭据已隐藏）' : value;
  }
}
