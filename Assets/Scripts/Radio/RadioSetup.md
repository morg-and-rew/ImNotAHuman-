# GameConfig.json — единственный источник правды

Вся логика сюжета и радио задаётся в `Resources/GameConfig.json`.

## Структура

```json
{
  "story": {
    "startTrigger": "auto",
    "startDelaySeconds": 2,
    "steps": [...]
  },
  "radio": {
    "staticClipPath": "Radio/Static",
    "events": [...]
  }
}
```

## story.startTrigger

- **"auto"** — сюжет стартует сам через `startDelaySeconds` после интро
- **"client_interact"** — сюжет стартует при нажатии E в зоне клиента

## story.steps

Массив шагов по порядку. Каждый шаг:

| Поле | Описание |
|------|----------|
| stepId | ID для логов |
| stepType | Dialogue \| GoToRadio \| GoWarehouse \| GoWarehouseWaitReturn \| ReturnToClient |
| conversationTitle | Имя Conversation (для Dialogue) |
| hintText | Подсказка |
| activateRadioEventIds | Массив ID (для GoToRadio — какие радио-события активировать) |
| showRadioHintOnEnter | Показать подсказку про радио |
| expireRadioOnEnter | Отменить активные радио-события |
| ... | Остальные поля по необходимости |

## radio.events

События для радио. Каждое:

| Поле | Описание |
|------|----------|
| eventId | ID (совпадает с activateRadioEventIds в шаге) |
| conversationTitle | Conversation для диалога внизу |
| priority | Приоритет при нескольких активных |
| audioPath | Путь в Resources для озвучки |

## radio.staticClipPath

Путь к клипу помех в Resources. Играет, когда активируется радио-событие.

## Инспектор Radio

- **Static Clip** — клип помех. Надёжнее, чем путь. Если пусто — используется путь из config.
- **Event Clips** — массив `{ eventId, clip }`. Для каждого сюжетного события (например `tutorial_radio`) можно указать клип напрямую. Заменяет `audioPath` из config.
- **Event Videos** — массив `{ eventId, videoClip }`. Если указан, по этому `eventId` на `E` в радио запускается видео (через `Video Player`) вместо диалога.
- **Video Player** — ссылка на `UnityEngine.Video.VideoPlayer`, который проигрывает ролик.
- **Video Root** — optional root объекта/канваса для видео, автоматически включается на время ролика.
- **Static Clip Path Override** — fallback, если Static Clip не задан
- **Station Source** — один AudioSource для всех станций (фоновая музыка в цикле)
- **Station Clips** — массив клипов станций (по одному на «кнопку» переключения)
- **Voice Source** — один AudioSource для помех (статик) и сюжетной озвучки (по очереди)
- **Hint** — спрайт подсказки
