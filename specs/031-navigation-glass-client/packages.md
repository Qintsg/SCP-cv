# 1.0.3客户端制品

日期：2026-10-30。实现提交`a278227a3285ba283de69501427d26b15b246798`已正常推送GitHub与GitLab main，GPG签名有效；制品从该提交的干净工作区构建。

## 桌面文件

| 制品 | 本机路径 | 字节数 | SHA256 |
| --- | --- | ---: | --- |
| Windows x64便携ZIP | `C:/Users/qintsg/Desktop/SCP-cv-Windows-x64-Portable-20261030.zip` | 303804208 | `246745E834D399D65FFAB2013C3787C8BD6DFFE1EA6F3EB7C1C4AAFBCA873632` |
| Android arm64-v8a手机 | `C:/Users/qintsg/Desktop/SCP-cv-Android-arm64-v8a-1.0.3.apk` | 5123670 | `A915FD007F9CB0395ACB6E1EE5625E9567527F679821DD6057FAC4EBABD98907` |
| Android arm64-v8a Pad | `C:/Users/qintsg/Desktop/SCP-cv-Pad-arm64-v8a-1.0.3.apk` | 5123670 | `A915FD007F9CB0395ACB6E1EE5625E9567527F679821DD6057FAC4EBABD98907` |

手机和Pad为同一个自适应应用的两份命名，支持横竖屏；包名与签名保持，可以更新既有应用。

## Windows核验

Electron44.2.0、程序版本1.0.3.0。正式构建比对109个应用代码文件和195个原生预览资产；ASAR摘要为`E8C1D51C69FD253B5A594E4586BAAEAAE60E1C9C33BB6861410144227CEA8C6F`。

ZIP含274个文件和7个目录，7-Zip完整性检查通过，根目录启动程序及ZIP内ASAR摘要与本轮目录包一致。ZIP根目录包含BUILD-INFO.json，记录实际实现提交。

证据：`.validation/navigation-031/windows-integrity.json`、`build-windows.log`、`zip-windows-check.log`。本机媒体资产继续沿用现有development-only范围，许可材料随包保留。

## Android核验

- 版本1.0.3、versionCode4、包名`cn.edu.sspu.scpcv.control`、arm64-v8a。
- Java21、SDK36、Gradle8.14.3；assembleRelease及lintRelease成功。Capacitor依赖沿用其既有Lint基线，未修改基线。
- zipalign的16KiB排列检查通过；apksigner的v2和v3签名检查通过。
- 原签名证书SHA256：`FD53EDBF1413E827214A971D47752162C94A76E2997EC15021D785834656E88B`。
- APK内89个网页文件逐一与当前dist-app摘要一致。
- 实际CA资源`res/Tj.crt`按内容摘要定位，SHA256为`B352C1456AC94F5D683A9C4079E7EB1506A1EE92B9F0D40B3B055AAEB6ADB36D`，与现场公开根证书一致。

证据目录：`.validation/navigation-031/android-20261030-142118908/`，含构建、签名、aapt元数据与逐文件检查报告。签名私钥及口令保持本机私有存储。

## 旧制品与运行范围

旧桌面Windows ZIP与1.0.2手机/Pad APK共三份已移入`.validation/navigation-031/desktop-before-20261030/`，逐份摘要保持，清单位于`desktop-before.json`。桌面保留三份当前制品。

没有更新D4服务、显示模式、现场设备或用户客户端配置。030新增后端接口仍需后续同步D4的ControlHost和PlayerWorker；真实Android手指操作及真实后台屏幕共享调度边界见[验证记录](verification.md)。
