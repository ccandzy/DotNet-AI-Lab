API Generate转Chat

Chat => Memory

 `chat的"message" : {"role": "assistant", "content": " Let"}, `

`generate的"response" : " you", `

`generate还会有一个content的字段。`

add Conversation

` conversation management `

Need improvement

ViewModel
      │
ChatMessage
      │
      ▼
IChatService
      │
      ▼
Mapper
      │
      ▼
Ollama DTO
