/** 前端显式TLS证书门禁：无配置保持HTTP，错误配置拒绝降级，不输出私钥。SCP-cv 2026-10-11。 */
import { createPrivateKey, X509Certificate } from 'node:crypto';
import { readFileSync, realpathSync, statSync } from 'node:fs';
import { isIP } from 'node:net';
import { extname, isAbsolute, relative, resolve, sep } from 'node:path';
import { createSecureContext } from 'node:tls';
import type { ServerOptions } from 'node:https';

/** :param value: 服务器访问主机列表。:returns: 显式DNS/IP，不能用URL或监听通配地址冒充证书身份。 */
function accessHosts(value: string): string[] {
  const hosts = value.split(',').map(host => host.trim());
  for (const host of hosts) {
    const dns = host.length <= 253 && host.split('.').every(label => label.length <= 63 && /^[a-z\d](?:[a-z\d-]*[a-z\d])?$/i.test(label));
    if (!host || host === '0.0.0.0' || host === '::' || (!isIP(host) && !dns)) {
      throw new Error('HTTPS访问主机必须是明确DNS名称或IP，不能含URL、端口、通配符或空项。');
    }
  }
  return [...new Set(hosts)];
}
/** :param filename: 已明确配置的路径。:param directory: frontend目录。:param label: 固定错误描述。:returns: 有界普通PEM文件；不回显路径、文件内容或解析器异常。 */
function readPem(filename: string, directory: string, label: string): Buffer {
  try {
    const requested = resolve(directory, filename), path = realpathSync(requested);
    // public不受Vite的fs.deny保护；同时核配置路径和实体路径，拒绝从frontend链接出去。
    for (const [base, candidate] of [[resolve(directory), requested], [realpathSync(directory), path]]) {
      const location = relative(base!, candidate!);
      const extensions = label === '证书' ? ['.pem', '.crt'] : ['.pem', '.key'];
      if (!(location.startsWith(`..${sep}`) || isAbsolute(location)) || !extensions.includes(extname(candidate!).toLowerCase())) throw new Error('invalid');
    }
    const info = statSync(path);
    if (!info.isFile() || info.size === 0 || info.size > 256 * 1024) throw new Error('invalid');
    return readFileSync(path);
  } catch { throw new Error(`HTTPS无法读取${label}文件：须为frontend目录外可读且不超过256KiB的普通.pem${label === '证书' ? '/.crt' : '/.key'}文件，不能存入public或使用其中的链接。`); }
}
/** :param env: 只在服务器加载的环境，不用VITE_前缀暴露配置。:param directory: frontend目录。:param now: 日期校验时刻。:returns: 明确TLS配置或完全无配置的undefined；部分/错误配置必须终止启动。 */
export function loadFrontendHttps(env: Record<string, string | undefined>, directory: string, now = Date.now()): ServerOptions | undefined {
  const certFile = env.SCP_CV_HTTPS_CERT_FILE?.trim() ?? '';
  const keyFile = env.SCP_CV_HTTPS_KEY_FILE?.trim() ?? '';
  const configuredHosts = env.SCP_CV_HTTPS_HOSTS?.trim() ?? '';
  if (!certFile && !keyFile && !configuredHosts) return undefined;
  if (!certFile || !keyFile || !configuredHosts) throw new Error('HTTPS必须同时提供SCP_CV_HTTPS_CERT_FILE、SCP_CV_HTTPS_KEY_FILE和SCP_CV_HTTPS_HOSTS。');
  const hosts = accessHosts(configuredHosts);
  const cert = readPem(certFile, directory, '证书'), key = readPem(keyFile, directory, '私钥');
  let certificate: X509Certificate;
  try { certificate = new X509Certificate(cert); }
  catch { throw new Error('HTTPS证书无效：需要PEM服务器证书及可选中间链。'); }
  if (certificate.ca) throw new Error('HTTPS不能把CA根证书用作服务器证书。');
  if (!Number.isFinite(now) || now < Date.parse(certificate.validFrom) || now >= Date.parse(certificate.validTo)) throw new Error('HTTPS证书不在有效期内。');
  if (certificate.keyUsage?.length && !certificate.keyUsage.includes('1.3.6.1.5.5.7.3.1')) throw new Error('HTTPS证书用途不允许服务器认证。');
  for (const host of hosts) {
    const matched = isIP(host) ? certificate.checkIP(host) : certificate.checkHost(host, { subject: 'never' });
    if (!matched) throw new Error('HTTPS证书SAN与配置的访问主机不匹配，请重新签发包含正确DNS/IP的证书。');
  }
  let privateKey;
  try { privateKey = createPrivateKey(key); }
  catch { throw new Error('HTTPS私钥无效：需要对应的未加密PEM私钥，仅保存在服务机器。'); }
  if (!certificate.checkPrivateKey(privateKey)) throw new Error('HTTPS证书与私钥不匹配。');
  const options: ServerOptions = { cert, key, minVersion: 'TLSv1.2' };
  try { createSecureContext(options); }
  catch { throw new Error('HTTPS证书链或私钥无法建立TLS上下文。'); }
  return options;
}
