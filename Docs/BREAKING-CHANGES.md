# 破坏性变更

## v1.5.1

此版本开始暂不兼容1080p以下分辨率

## v1.5.0

从此版本开始不再在同步时创建封面缩略图，升级此版本后需要手动（保险起见）删除历史缩略图，可以通过【系统设置】-【未知文件】-【查找并清理】进行。

## v1.1.0

### 分类
由于数据结构有较大变化，所有分类的~~解析器~~已被移除，需要设置对应的[组件](https://github.com/Bakabase/InsideWorld/blob/main/Docs/DEFINITIONS.md#%E7%BB%84%E4%BB%B6)确保分类的状态为![image](https://user-images.githubusercontent.com/2888789/147025320-15369813-b9dd-44e1-b268-c32938423d39.png)时才可继续使用

### 同步产生的临时文件
在**未设置加速路径**的情况下，以前版本产生的临时文件未保存到正确的地址，并且会随每次安装被清空，故升级该版本后，需要手动重新同步媒体库。详见[#24](https://github.com/Bakabase/InsideWorld/issues/24)
