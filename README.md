# com.et.module.addressables

> 该模块主要基于传统更新模式实现的资源更新。主要用于软件启动时比对资源、下载资源，使用方式如下：
>
> ~~~
> await Game.Scene.GetComponent<AddressablesComponent>().StartAsync();
> await Game.Scene.GetComponent<AddressablesComponent>().DownloadAsync();
> ~~~
>
> 组件~AddressablesComponent~内提供下载进度供查询用。

