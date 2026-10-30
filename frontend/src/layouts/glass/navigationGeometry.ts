// 文件描述：将真实底栏按钮的屏幕坐标换算为连续入口位置。
// 项目：SCP-cv；文件：navigationGeometry.ts；作者：Qintsg；日期：2026-10-30。
export interface NavigationSlotGeometry { readonly left: number; readonly right: number }

/**
 * :param clientX: PointerEvent提供的当前视口横坐标。
 * :param slots: 按页面顺序读取的真实按钮边界。
 * :returns: 相邻中心之间的连续索引，首末中心之外保持首末入口。
 */
export function navigationPosition(clientX: number, slots: readonly NavigationSlotGeometry[]): number {
  if (slots.length === 0) throw new RangeError('底栏入口不存在。');
  let previous = (slots[0]!.left + slots[0]!.right) / 2;
  if (clientX <= previous) return 0;
  for (let index = 1; index < slots.length; index++) {
    const center = (slots[index]!.left + slots[index]!.right) / 2;
    if (clientX <= center) return index - 1 + (clientX - previous) / (center - previous);
    previous = center;
  }
  return slots.length - 1;
}
