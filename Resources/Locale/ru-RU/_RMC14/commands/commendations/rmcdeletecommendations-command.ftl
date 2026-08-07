cmd-rmcdeletecommendations-desc = Удаляет награды по раунду, выдавшему, получателю или ID.
cmd-rmcdeletecommendations-help = Использование:
  rmcdeletecommendations id <commendationId>
    - Удалит одну награду с помощью ID

  rmcdeletecommendations round <roundId> <type>
    - Удаляет все награды за конкретный раунд и тип.
    - Используйте: type commendation filter

  rmcdeletecommendations round <roundId> <type> giver <usernameOrId>
    - Удаляет награды, выданные игроком в раунде и определенного типа.
    - Используйте: type commendation filter

  rmcdeletecommendations round <roundId> <type> receiver <usernameOrId>
    - Удаляет награды, выданные игроку в раунде и определенного типа.
    - Используйте: type commendation filter

  Примеры:
    rmcdeletecommendations id 128
    rmcdeletecommendations round 42 medal
    rmcdeletecommendations round 42 jelly giver PlayerName
    rmcdeletecommendations round 42 medal receiver PlayerName

cmd-rmcdeletecommendations-invalid-arguments = Неверные аргументы!
cmd-rmcdeletecommendations-invalid-round-id = Неверный ID раунда!
cmd-rmcdeletecommendations-invalid-id = Неверный ID медали!
cmd-rmcdeletecommendations-invalid-type = Неверный тип '{ $type }'!
cmd-rmcdeletecommendations-invalid-player-mode = Неверный тип игрока! Должно быть 'giver' или 'receiver'.
cmd-rmcdeletecommendations-player-not-found = Игрок '{ $player }' не найден.
cmd-rmcdeletecommendations-no-results = Наград не найдено.

cmd-rmcdeletecommendations-id-header = Награда { $id } удалена:
cmd-rmcdeletecommendations-round-header = Удалены медали из раунда { $round } ({ $count } всего):
cmd-rmcdeletecommendations-format = id [{ $id }] { $type }: { $name } — { $giverUserName } ({ $giver }) → { $receiverUserName } ({ $receiver }) Раунд { $round }: { $text }
cmd-rmcdeletecommendations-admin-announcement = { $admin } удалил награды с ID: { $ids }
cmd-rmcdeletecommendations-admin-announcement-round = { $admin } удалил медали из раунда { $round }. Удаленный ID: { $ids }

cmd-rmcdeletecommendations-hint-mode = Тип (id или раунд)
cmd-rmcdeletecommendations-hint-mode-id = Удалить награду по ID
cmd-rmcdeletecommendations-hint-mode-round = Удалить награды из раунда
cmd-rmcdeletecommendations-hint-round-id = ID раунда
cmd-rmcdeletecommendations-hint-commendation-id = ID награды
cmd-rmcdeletecommendations-hint-type = Тип награды
cmd-rmcdeletecommendations-hint-player-mode = Тип игрока (выдавший или получивший)
cmd-rmcdeletecommendations-hint-player-giver = Награды выданы игроком
cmd-rmcdeletecommendations-hint-player-receiver = Награды получены игроком
cmd-rmcdeletecommendations-hint-player = CKey или ID пользователя
