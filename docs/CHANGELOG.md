## [1.7.3-beta3](https://cdn-public.anobaka.com/app/bakabase/inside-world/1.7.3-beta3/installer/Bakabase.InsideWorld.zip) (2024-01-18)

### Features

* 优化缩略图性能 ([#477](https://github.com/anobaka/InsideWorld/issues/477))
* 将创建分类流程里的文件(夹)选择器替换为react版本 ([#479](https://github.com/anobaka/InsideWorld/issues/479))
* 现在在配置媒体库路径信息时，可以选择文件夹作为样例路径 ([#480](https://github.com/anobaka/InsideWorld/issues/480))
* 优化部分界面交互 ([#481](https://github.com/anobaka/InsideWorld/issues/481))
* 优化资源列表页的交互和性能 ([#482](https://github.com/anobaka/InsideWorld/issues/482))

### Bug Fixes

* Javlibrary增强器返回多个结果时不匹配 ([#476](https://github.com/anobaka/InsideWorld/issues/476))
* 某些情况下创建分类时会报错：ComponentKey field is required ([#483](https://github.com/anobaka/InsideWorld/issues/483))
* 某些情况下，为媒体库指定了固定标签时会在同步媒体库时报错 ([#484](https://github.com/anobaka/InsideWorld/issues/484))

## [1.7.3-beta2](https://cdn-public.anobaka.com/app/bakabase/inside-world/1.7.3-beta2/installer/Bakabase.InsideWorld.zip) (2023-11-28)

该版本包含[破坏性变更](./BREAKING-CHANGES.md)，在仔细阅读相关说明后再升级至该版本；

### Bug Fixes

* 如果使用正则表达式进行资源过滤，当资源A的文件名是资源B的前缀时，资源B将会被忽略 ([#467](https://github.com/anobaka/InsideWorld/issues/467))
* 文件处理器仅显示文件夹，未显示文件 ([#474](https://github.com/anobaka/InsideWorld/issues/474))

## [1.7.3-beta](https://cdn-public.anobaka.com/app/bakabase/inside-world/1.7.3-beta/installer/Bakabase.InsideWorld.zip) (2023-11-27)

该版本包含[破坏性变更](./BREAKING-CHANGES.md)，在仔细阅读相关说明后再升级至该版本；

### Features

* 将文件(夹)选择控件转移至react ([#444](https://github.com/anobaka/InsideWorld/issues/444))
* 自动或由用户触发下载/更新组件 ([#446](https://github.com/anobaka/InsideWorld/issues/446))
* 优化文件移动器 ([#463](https://github.com/anobaka/InsideWorld/issues/463))
* “封面大图”去掉勾选后无法记忆，下次还会默认选中 ([#466](https://github.com/anobaka/InsideWorld/issues/466))

### Bug Fixes

* 更换哔哩哔哩下载器登录验证api ([#468](https://github.com/anobaka/InsideWorld/issues/468))
* 修复Cookie验证器在第二次验证时会报错的问题 ([#469](https://github.com/anobaka/InsideWorld/issues/469))
* 下载器页面Label显示异常 ([#470](https://github.com/anobaka/InsideWorld/issues/470))

## [1.7.2](https://cdn-public.anobaka.com/app/bakabase/inside-world/1.7.2/installer/Bakabase.InsideWorld.zip) (2023-10-01)

### Features

* 鼠标悬浮在资源封面一段时间后，自动预览资源内容 ([#364](https://github.com/anobaka/InsideWorld/issues/364))
* 在资源详情页点击标签进行搜索 ([#422](https://github.com/anobaka/InsideWorld/issues/422))
* 在资源列表移动资源时自动将资源同步至目标媒体库 ([#366](https://github.com/anobaka/InsideWorld/issues/366))
* 完善封面设置功能 ([#428](https://github.com/anobaka/InsideWorld/issues/428))
* 当使用外置播放器并且可播放文件数量>1时，显示的播放文件路径过长 ([#437](https://github.com/anobaka/InsideWorld/issues/437))
* 深色模式 ([#84](https://github.com/anobaka/InsideWorld/issues/84))
* 通过自定义属性搜索资源支持部分文本匹配 ([#442](https://github.com/anobaka/InsideWorld/issues/442))
* 增加临时文件目录 ([#440](https://github.com/anobaka/InsideWorld/issues/440))
* 优化媒体库路径配置和验证步骤 ([#453](https://github.com/anobaka/InsideWorld/issues/453))
* 移除自动同步 ([#455](https://github.com/anobaka/InsideWorld/issues/455))
* 可以在设置中调整默认封面保存路径 ([#457](https://github.com/anobaka/InsideWorld/issues/457))
* 调整看板页面 ([#458](https://github.com/anobaka/InsideWorld/issues/458))
* 调整增强器产生的封面路径 ([#456](https://github.com/anobaka/InsideWorld/issues/456))
* 限制封面缓存 ([#464](https://github.com/anobaka/InsideWorld/issues/464))

### Bug Fixes

* 子资源封面无法加载 ([#438](https://github.com/anobaka/InsideWorld/issues/438))
* 资源匹配器中正则匹配的路径异常 ([#441](https://github.com/anobaka/InsideWorld/issues/441))
* 正则匹配不生效 ([#436](https://github.com/anobaka/InsideWorld/issues/436))
* 防止路径匹配器弹窗超出屏幕 ([#439](https://github.com/anobaka/InsideWorld/issues/439))
* 调整部分深色模式的颜色 ([#448](https://github.com/anobaka/InsideWorld/issues/448))
* 资源列表，点击筛选条件的选项后未立即搜索 ([#450](https://github.com/anobaka/InsideWorld/issues/450))
* 通过正则表达式匹配资源会包含子文件 ([#451](https://github.com/anobaka/InsideWorld/issues/451))
* 删除媒体库根目录时，会将该媒体库内所有相同根目录的配置一起删除 ([#452](https://github.com/anobaka/InsideWorld/issues/452))
* 同步过程中，如果新增标签与历史标签大小写不一致，其他内容相同，则同步会失败 ([#454](https://github.com/anobaka/InsideWorld/issues/454))
* 配置界面验证exhentai cookie时没有提示 ([#459](https://github.com/anobaka/InsideWorld/issues/459))
* 文件移动器会多产生一级文件夹 ([#460](https://github.com/anobaka/InsideWorld/issues/460))
* DLsite增强器，rate_average_2dp为null时会增强失败 ([#461](https://github.com/anobaka/InsideWorld/issues/461))
* 资源筛选界面，通过【系列】筛选未生效 ([#462](https://github.com/anobaka/InsideWorld/issues/462))
* 文件处理器，部分环境下解压遇到不支持的字符时结果异常 ([#465](https://github.com/anobaka/InsideWorld/issues/465))

## [1.7.1](https://cdn-public.anobaka.com/app/bakabase/inside-world/1.7.1/installer/Bakabase.InsideWorld.zip) (2023-08-18)

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
* 支持本地网络文件的添加 ([#399](https://github.com/anobaka/InsideWorld/issues/399))
* 增加退出交互配置 ([#417](https://github.com/anobaka/InsideWorld/issues/417))

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
* 在创建分类时将非增强器组件加入到了增强器优先级的配置中，导致资源增强失败 ([#410](https://github.com/anobaka/InsideWorld/issues/410))
* 文件处理器 ctrl+a全选后列表的显示状态未变更 ([#409](https://github.com/anobaka/InsideWorld/issues/409))
* 资源界面筛选bug ([#408](https://github.com/anobaka/InsideWorld/issues/408))
* 在首次发起http请求的2分钟后报错：Cannot access a disposed object. Object name: 'SocketsHttpHandler' ([#413](https://github.com/anobaka/InsideWorld/issues/413))
* 文件处理器，包裹文件功能无法使用 ([#414](https://github.com/anobaka/InsideWorld/issues/414))
* 文件过多时，内层文件夹会出现横向滚动条 ([#407](https://github.com/anobaka/InsideWorld/issues/407))
* 内置Potplayer在安装了Potplayer后启动报错 ([#416](https://github.com/anobaka/InsideWorld/issues/416))
* 新版路径匹配器在同步时未正确填充资源属性、父级资源定位错误 ([#418](https://github.com/anobaka/InsideWorld/issues/418))
* 修改资源标签后UI未立即更新 ([#420](https://github.com/anobaka/InsideWorld/issues/420))
* 同步时丢失已录入数据 ([#421](https://github.com/anobaka/InsideWorld/issues/421))
* 解决部分视频ffmpeg没有成功获取截图的问题 ([#412](https://github.com/anobaka/InsideWorld/issues/412))
* 添加资源时按文件类型筛选不生效 ([#423](https://github.com/anobaka/InsideWorld/issues/423))
* 如果资源路径没变，但所属媒体库发生了变化，在下次同步时依旧会被归于原媒体库 ([#426](https://github.com/anobaka/InsideWorld/issues/426))
* 添加资源时按文件类型筛选无结果 ([#425](https://github.com/anobaka/InsideWorld/issues/425))
* InsideWorld增强器增强结果有误 ([#429](https://github.com/anobaka/InsideWorld/issues/429))
* 资源搜索界面，通过媒体库筛选资源时，如果分类名称过长，会和媒体库名称重叠 ([#430](https://github.com/anobaka/InsideWorld/issues/430))
* 资源根目录设置为磁盘根目录时同步结果不正确 ([#432](https://github.com/anobaka/InsideWorld/issues/432))
* 视频资源库获取封面时占用过高 ([#433](https://github.com/anobaka/InsideWorld/issues/433))
