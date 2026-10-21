/** 用户2026-10-21设备表的原型目录；状态是演示夹具，绝不探测现场。 */
export interface DemoInput { ip: number; name: string; aliases: string[]; group: 'workstation' | 'camera' | 'site'; available: boolean }
export const workstations: DemoInput[] = Array.from({ length: 24 }, (_, i) => ({
  ip: 51 + i, name: `D${Math.floor(i / 4) + 1}-${i % 4 + 1}`,
  aliases: i >= 12 && i <= 15 ? [`窗口 ${i - 11}`] : [], group: 'workstation', available: ![56, 58, 62, 67, 70, 74].includes(51 + i),
}));
export const cameras: DemoInput[] = [
  { ip: 84, name: '摄像机后', available: false }, { ip: 85, name: '摄像机中1', available: true },
  { ip: 86, name: '摄像机中2', available: true }, { ip: 87, name: '摄像机左', available: true },
  { ip: 88, name: '摄像机右', available: true }, { ip: 76, name: '监控01', available: true },
].map(item => ({ ...item, group: 'camera', aliases: [] }));
export const inputs: DemoInput[] = [...workstations, ...cameras, { ip: 81, name: '现场输入', aliases: ['IP81', '笔记本'], group: 'site', available: true }];
/** :param ip: 输入末段。:returns: 规范项；自定义只创建内存项。 */
export function inputFor(ip: number): DemoInput { return inputs.find(item => item.ip === ip) ?? { ip, name: '自定义输入', aliases: [], group: 'site', available: true }; }
/** :param item: 候选。:param query: 搜索词。:returns: 仅按名称、别名和IP末段匹配。 */
export function matches(item: DemoInput, query: string): boolean { return `${item.name} IP${item.ip} ${item.aliases.join(' ')}`.toLowerCase().includes(query.trim().toLowerCase()); }
/** :param ip: 规范IP末段。:returns: 同IP的组播示意，不保证实际设备已配置。 */
export function multicast(ip: number): string { return `224.1.1.${ip}:5000`; }
