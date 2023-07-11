现在可以进行更为复杂的资源路径配置

#### 1. 基础概念
+ **根目录**：仅且必须设置一个，仅能通过`路径`设置；
+ **资源**：仅且必须设置一个，可以通过`路径`或`正则表达式`设置；
+ **其他属性**：可不设置，可通过`路径`或`正则表达式`设置，如果该属性不能超过`1个`，则会默认使用第一个匹配到的结果；
+ **通过路径配置**：可以通过在`根目录`和`资源`之间，正序或倒序的目录层级来设置属性；
+ **通过正则表达式配置**：可以通过匹配在`根目录`和`资源`之间的路径字符串来设置属性；

#### 2. 设置界面
当属性不可用时，可以将鼠标移至不能设置的属性按钮上的设置方式来查看原因，有些提示也会出现在完整路径的正上方或正下方；

**可用性提示**

![categary-path-segment-matcher-1](/img/categary-path-segment-matcher-1.png)

**通过层级设置**

![categary-path-segment-matcher-2](/img/categary-path-segment-matcher-2.png)

**通过正则设置**

![categary-path-segment-matcher-3](/img/categary-path-segment-matcher-3.png)

一些其他设置样例

+ 简单匹配全部文本
![categary-path-segment-matcher-3-1](/img/categary-path-segment-matcher-3-1.png)

+ 匹配a和b
![categary-path-segment-matcher-3-2](/img/categary-path-segment-matcher-3-2.png)

+ 匹配b和c
![categary-path-segment-matcher-3-3](/img/categary-path-segment-matcher-3-3.png)

+ 匹配a、b和c
![categary-path-segment-matcher-3-4](/img/categary-path-segment-matcher-3-4.png)

**设置结果**

![categary-path-segment-matcher-4](/img/categary-path-segment-matcher-4.png)

**使用本地资源预览**

![categary-path-segment-matcher-5](/img/categary-path-segment-matcher-5.png)