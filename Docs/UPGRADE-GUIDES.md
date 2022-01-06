# 更新说明

## v1.3.0

### 资源

因为**资源**的关键属性`Directory`与`RawName`发生变动，为防止老版本已经创建的资源被删除（关联的标签也会一起删除），更新后首次启动时会自动迁移老数据，耗时参考：30_000个资源大约需要50秒。
详见[#51](https://github.com/Bakabase/InsideWorld/issues/51)
