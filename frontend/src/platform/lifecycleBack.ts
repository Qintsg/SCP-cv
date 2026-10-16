/** 原生返回键与真实Ant弹层协作，不向背景页面穿透操作。 */
const DISMISSIBLE_LAYER_SELECTOR = '[role="dialog"]';
const CLOSE_CONTROL_SELECTOR = 'button[aria-label="Close"], button[aria-label="close"]';

export type DismissibleLayerRoot = Pick<ParentNode, 'querySelectorAll'>;

/** :param element: 候选弹层。:returns: 可见层的祖先层级；隐藏缓存返回null。 */
function visibleLayerOrder(element: HTMLElement): number | null {
  let order = 0;
  for (let node: HTMLElement | null = element; node; node = node.parentElement) {
    if (node.hidden || node.getAttribute('aria-hidden') === 'true'
      || node.getAttribute('data-scp-dialog-open') === 'false') return null;
    const style = node.ownerDocument.defaultView?.getComputedStyle(node);
    if (style?.display === 'none' || style?.visibility === 'hidden') return null;
    order = Math.max(order, Number.parseInt(style?.zIndex || '0', 10) || 0);
  }
  return order;
}

/**
 * 关闭最上层可关闭交互。原生返回键不能依赖合成 Escape 事件，因为组件库只会
 * 接受浏览器生成的受信任键盘事件；显式调用关闭控件才能复用页面自身的关闭逻辑。
 */
export function dismissTopLayer(root: DismissibleLayerRoot = document): boolean {
  const layers = Array.from(root.querySelectorAll<HTMLElement>(DISMISSIBLE_LAYER_SELECTOR))
    .map(element => ({ element, order: visibleLayerOrder(element) }))
    .filter((item): item is { element: HTMLElement; order: number } => item.order !== null)
    .sort((left, right) => left.order - right.order);
  const layer = layers.at(-1)?.element;
  if (!layer) return false;

  layer.querySelector<HTMLElement>(CLOSE_CONTROL_SELECTOR)?.click();
  return true;
}
