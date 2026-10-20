# 018 内存模型

- PreviewInput：source_id或input互斥，quality为low／high；仅服务器解析实际地址。
- PreviewLease：随机GUID、Owner（用户／协议／登录Cookie摘要）、Origin、过期时间、消费连接代次；只允许一名消费者，续租不创建第二份。
- SharedFeed：上游SHA256键、唯一所有者路径、生产任务、订阅集合、档位、退出令牌、最后消费时间；同键创建在统一锁下只发生一次。
- FlvGop：完整AVC初始化与关键帧起始tag序列、单调时间戳、字节数；有严格大小上限，同份不可变缓冲供多消费者共享。
- PreviewBudget：出站总配额／当前消费者及流数／拥塞降档与恢复窗口；仅预览，不更改正式播放。

无数据库表。创建租约→消费→续租→离屏／失联／关闭→回收；无观众→停止转换器→MediaMTX路径释放。旧异步清理只能操作原SharedFeed实例，不能删除同键的新代次。
