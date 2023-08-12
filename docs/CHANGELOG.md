## [1.7.1-beta6](https://github.com/anobaka/InsideWorld/releases/tag/v1.7.1-beta6) (2023-08-12)

### Bug Fixes

* 添加资源时按文件类型筛选不生效 #423 ([#423](https://github.com/anobaka/InsideWorld/issues/423))

## [1.7.1-beta5](https://github.com/anobaka/InsideWorld/releases/tag/v1.7.1-beta5) (2023-08-08)

### Bug Fixes

* 内置Potplayer在安装了Potplayer后启动报错 ([#416](https://github.com/anobaka/InsideWorld/issues/416))
* 新版路径匹配器在同步时未正确填充资源属性、父级资源定位错误 ([#418](https://github.com/anobaka/InsideWorld/issues/418))
* 修改资源标签后UI未立即更新 ([#420](https://github.com/anobaka/InsideWorld/issues/420))
* 同步时丢失已录入数据 ([#421](https://github.com/anobaka/InsideWorld/issues/421))
* 解决部分视频ffmpeg没有成功获取截图的问题 ([#412](https://github.com/anobaka/InsideWorld/issues/412))

### Features

* 增加退出交互配置 ([#417](https://github.com/anobaka/InsideWorld/issues/417))


## [1.7.1-beta4](https://github.com/anobaka/InsideWorld/releases/tag/v1.7.1-beta4) (2023-07-28)

### Bug Fixes

* 文件处理器，包裹文件功能无法使用 ([#414](https://github.com/anobaka/InsideWorld/issues/414))
* 文件过多时，内层文件夹会出现横向滚动条 ([#407](https://github.com/anobaka/InsideWorld/issues/407))


## [1.7.1-beta3](https://github.com/anobaka/InsideWorld/releases/tag/v1.7.1-beta3) (2023-07-25)

### Bug Fixes

* 在首次发起http请求的2分钟后报错：Cannot access a disposed object. Object name: 'SocketsHttpHandler' ([#413](https://github.com/anobaka/InsideWorld/issues/413))


## [1.7.1-beta2](https://github.com/anobaka/InsideWorld/releases/tag/v1.7.1-beta2) (2023-07-22)

### Bug Fixes

* 在创建分类时将非增强器组件加入到了增强器优先级的配置中，导致资源增强失败 ([#410](https://github.com/anobaka/InsideWorld/issues/410))
* 文件处理器 ctrl+a全选后列表的显示状态未变更 ([#409](https://github.com/anobaka/InsideWorld/issues/409))
* 资源界面筛选bug ([#408](https://github.com/anobaka/InsideWorld/issues/408))

### Features

* 支持本地网络文件的添加 ([#399](https://github.com/anobaka/InsideWorld/issues/399))


## [1.7.1-beta](https://github.com/anobaka/InsideWorld/releases/tag/v1.7.1-beta) (2023-07-12)

### Bug Fixes

* 部分配置保存后未立即生效 ([#415](https://github.com/anobaka/InsideWorld/issues/415))
* 哔哩哔哩下载器获取收藏夹失败 ([#402](https://github.com/anobaka/InsideWorld/issues/402))
* virtual-list node被移除再重新初始化后，未获取到当前操作的进度（如解压缩） ([#385](https://github.com/anobaka/InsideWorld/issues/385))
* 文件夹内的文件和文件夹同名的情况下，向外提取后该entry的属性还是文件夹，需要重新刷新才能变成文件 ([#384](https://github.com/anobaka/InsideWorld/issues/384))
* 文件处理器的entry偶尔会被其他透明内容遮挡，导致部分操作很难点击 ([#383](https://github.com/anobaka/InsideWorld/issues/383))
* 悬浮助手在未弹出任务列表时实际占用的高度和弹出任务列表时一致，导致部分操作会被遮挡 ([#396](https://github.com/anobaka/InsideWorld/issues/396))
* 文件处理器的包裹功能失效 ([#377](https://github.com/anobaka/InsideWorld/issues/377))
* 当文件路径包含感叹号时，内置播放器无法正确播放 ([#376](https://github.com/anobaka/InsideWorld/issues/376))
* 内置播放器，部分遮罩区域点击后播放器不会关闭 ([#375](https://github.com/anobaka/InsideWorld/issues/375))
* 媒体库配置路径时，如果选择了“根目录后第N层文件(夹)是资源”，当N>1时，下次再查看时会变成自定义正则 ([#372](https://github.com/anobaka/InsideWorld/issues/372))

### Features

* 优化播放器文件列表 ([#406](https://github.com/anobaka/InsideWorld/issues/406))
* 分类列表的排序顺序从【手动指定>创建时间】变更为【手动指定>名称】 ([#405](https://github.com/anobaka/InsideWorld/issues/405))
* 尝试从exe文件提取封面图 ([#403](https://github.com/anobaka/InsideWorld/issues/403))
* 优化后台小助手 ([#401](https://github.com/anobaka/InsideWorld/issues/401))
* 优化资源列表页面 ([#350](https://github.com/anobaka/InsideWorld/issues/350))
* 优化文件处理器 ([#400](https://github.com/anobaka/InsideWorld/issues/400))
* 增加全局代理配置 ([#397](https://github.com/anobaka/InsideWorld/issues/397))
* 配置標籤增加正則表達式的選項 ([#398](https://github.com/anobaka/InsideWorld/issues/398))
* 为自动移动文件功能增加延迟选项 ([#391](https://github.com/anobaka/InsideWorld/issues/391))
* 下载任务批量控制功能 ([#387](https://github.com/anobaka/InsideWorld/issues/387))
* 增加当不同的下载任务产生同名的下载目录时的处理选项 ([#388](https://github.com/anobaka/InsideWorld/issues/388))
* 存储历史解压密码 ([#394](https://github.com/anobaka/InsideWorld/issues/394))
* 优化删除全部同名文件的弹窗样式和交互 ([#395](https://github.com/anobaka/InsideWorld/issues/395))
* 点击标签可以搜索包含该标签的资源 ([#379](https://github.com/anobaka/InsideWorld/issues/379))
* 解压时移除文件名前后空白 ([#382](https://github.com/anobaka/InsideWorld/issues/382))
* 文件处理器 悬浮复制文件名会遮挡其他按钮 ([#393](https://github.com/anobaka/InsideWorld/issues/393))
* 为媒体库各路径增加快速打开的功能 ([#374](https://github.com/anobaka/InsideWorld/issues/374))
* 优化组件结构 ([#363](https://github.com/anobaka/InsideWorld/issues/363))
* 支持基于自定义正则表达式的增强器 ([#240](https://github.com/anobaka/InsideWorld/issues/240))
* 增加单个资源的增强功能 ([#371](https://github.com/anobaka/InsideWorld/issues/371))
* 在InsideWorld增强器中应用自定义日期解析规则 ([#370](https://github.com/anobaka/InsideWorld/issues/370))
* 在JavLibrary增强器中应用自定义日期解析规则 ([#369](https://github.com/anobaka/InsideWorld/issues/369))
* 在DLsite增强器中应用自定义日期解析规则 ([#368](https://github.com/anobaka/InsideWorld/issues/368))
* 增加配置自定义日期格式的能力 ([#367](https://github.com/anobaka/InsideWorld/issues/367))
* pixiv下载器 ([#307](https://github.com/anobaka/InsideWorld/issues/307))
* bangumi增强器 ([#246](https://github.com/anobaka/InsideWorld/issues/246))
* 希望标签能有反选功能 ([#329](https://github.com/anobaka/InsideWorld/issues/329))