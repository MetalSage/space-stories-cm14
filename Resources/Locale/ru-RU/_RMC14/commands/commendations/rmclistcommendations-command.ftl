# List Commendations Command
cmd-rmclistcommendations-desc = Отображает список наград по раунду, игроку, ID или недавним записям.
cmd-rmclistcommendations-help = Использование:
  rmclistcommendations last <count> [type]
    - Список последних наград
    - count: количество последних наград для отображения
    - type: тип награды (Все по умолчанию)

  rmclistcommendations round <roundId> [type]
    - Отображает все награды за конкретный раунд.
    - type: тип награды (Все по умолчанию)

  rmclistcommendations id <commendationId>
    - Указывается одна награда по ID

  rmclistcommendations player giver <usernameOrId> <count> [type]
    - Отображает список наград, выданных игроком
    - count: количество последних наград для отображения
    - type: тип награды (Все по умолчанию)

  rmclistcommendations player receiver <usernameOrId> <count> [type]
    - Отображает список наград, полученных игроком
    - count: количество последних наград для отображения
    - type: тип награды (Все по умолчанию)

  Примеры:
    rmclistcommendations last 10
    rmclistcommendations last 5 jelly
    rmclistcommendations round 42
    rmclistcommendations round 42 medal
    rmclistcommendations id 128
    rmclistcommendations player giver PlayerName 10
    rmclistcommendations player receiver PlayerName 5 jelly

# Errors
cmd-rmclistcommendations-invalid-arguments = Неверные аргументы!
cmd-rmclistcommendations-invalid-round-id = Неверный ID раунда!
cmd-rmclistcommendations-invalid-id = Неверный ID награды!
cmd-rmclistcommendations-invalid-type = Неверный тип '{ $type }'!
cmd-rmclistcommendations-invalid-player-mode = Неверный тип игрока! Должно быть 'giver' или 'receiver'.
cmd-rmclistcommendations-invalid-count = Неверное количество! Должно быть положительное число.
cmd-rmclistcommendations-player-not-found = Игрок '{ $player }' не найден.
cmd-rmclistcommendations-no-results = Наград не найдено.

# Headers
cmd-rmclistcommendations-last-header = Отображено { $count } последних выданных наград (запрошено: { $total }):
cmd-rmclistcommendations-round-header = Награды за раунд { $round } ({ $count } всего):
cmd-rmclistcommendations-id-header = Награда { $id }:
cmd-rmclistcommendations-giver-header = Отображено { $count } последних выданных наград (запрошено: { $total }):
cmd-rmclistcommendations-receiver-header = Отображено { $count } последних полученных наград (запрошено: { $total }):

# Format
cmd-rmclistcommendations-format = id [{ $id }] { $type }: { $name } — { $giverUserName } ({ $giver }) → { $receiverUserName } ({ $receiver }) Раунд { $round }: { $text }

# Completion hints
cmd-rmclistcommendations-hint-mode = Тип (последних, раунд, ID медали или CKey игрока)
cmd-rmclistcommendations-hint-mode-last = Список N последних выданных наград
cmd-rmclistcommendations-hint-mode-round = Список наград за раунд
cmd-rmclistcommendations-hint-mode-id = Список наград по ID
cmd-rmclistcommendations-hint-mode-player = Список наград игрока
cmd-rmclistcommendations-hint-round-id = ID раунда
cmd-rmclistcommendations-hint-commendation-id = ID награды
cmd-rmclistcommendations-hint-player-mode = Тип игрока (выдавший или получивший)
cmd-rmclistcommendations-hint-player-giver = Выданные игроком награды
cmd-rmclistcommendations-hint-player-receiver = Полученные игроком награды
cmd-rmclistcommendations-hint-player = CKey или ID игрока
cmd-rmclistcommendations-hint-count = Количество отображаемых наград
cmd-rmclistcommendations-hint-type = Фильтр наград по типу
