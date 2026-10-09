// 播放器主题与测试友元；友元只用于替换外部引擎adapter，不新增运行控制入口。
using System.Windows;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("ScpCv.Windows.Tests")]

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
