## AI Role

System pormpt

###### Role

你是谁？

###### Expertise

你擅长什么？

###### Goal

你的目标是什么？

###### Behavior

你应该如何行动？

###### Output Format

回答格式是什么？

###### Constraint

有什么限制？

###### MarkDown+代码高亮

实现路线：WPF嵌套WebView. 使用HTML的方式实现。每次动态生成XML，进行渲染。



使用第三方JS：`highlightAll` 方法 进行代码高亮区分



<mark>存在的问题：</mark> 因为聊天记录使用ItemControl包装，每个item使用单独的HTML渲染。导致滚动时候，HTML内容跟本身WPF内容 重合。  可能需要整个聊天记录使用HTML渲染
