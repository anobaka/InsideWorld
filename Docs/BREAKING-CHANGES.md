# 破坏性变更

## 1.1.0

### 分类
由于数据结构有较大变化，所有分类的~~解析器~~已被移除，需要设置对应的[组件](https://github.com/Bakabase/InsideWorld/blob/main/Docs/DEFINITIONS.md#%E7%BB%84%E4%BB%B6)确保分类的状态为**可用**时才可继续使用

### 同步产生的临时文件
在**未设置加速路径**的情况下，以前版本产生的临时文件未保存到正确的地址，并且会随每次安装被清空，故升级该版本后，需要手动重新同步媒体库。详见[#24](https://github.com/Bakabase/InsideWorld/issues/24)
