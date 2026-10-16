/** 弹层交接焦点：忽略关闭缓存，不让旧离场回调抢走新弹层的键盘位置。 */

/** :param element: 入口或弹层。:returns: 祖先链未被隐藏或关闭。 */
function visible(element: HTMLElement): boolean {
  for (let node: HTMLElement | null = element; node; node = node.parentElement) {
    const style = getComputedStyle(node);
    if (node.hidden || node.getAttribute('aria-hidden') === 'true'
      || node.getAttribute('data-scp-dialog-open') === 'false'
      || style.display === 'none' || style.visibility === 'hidden') return false;
  }
  return true;
}

/** :param trigger: 原可见入口。:param closing: 当前关闭的弹层根。:returns: 保留新弹层焦点或安全返回原入口。 */
export function restoreDialogFocus(trigger: HTMLElement | null, closing: Element | null): void {
  const others = [...document.querySelectorAll<HTMLElement>('[role="dialog"][aria-modal="true"]')]
    .filter(layer => !closing?.contains(layer) && visible(layer));
  if (trigger?.isConnected && visible(trigger) && (!others.length || others.some(layer => layer.contains(trigger)))) {
    trigger.focus({ preventScroll: true });
  } else if (others.length && !others.some(layer => layer.contains(document.activeElement))) {
    others.at(-1)?.focus({ preventScroll: true });
  }
}
