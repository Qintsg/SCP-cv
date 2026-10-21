# 内存模型

- DemoInput：ip末段唯一键、名称、别名、类别和模拟连通；窗口1–4指63–66，与D4工作站同键。
- DemoBlock：稳定id、ip、column／row／columns／rows；几何不参与流key。
- DemoFeed：canvas、MediaStream、引用数、帧数、创建序号和计时器；最后引用离开释放，隐藏暂停绘制，退出统一释放。
- PickerState：variant=A/B/C、类别、搜索、当前目标块；切变体不重置布局与流池。

不写数据库，不建立设备套接字。
