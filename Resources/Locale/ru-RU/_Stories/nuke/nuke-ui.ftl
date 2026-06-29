st-nuke-ui-decryption-status =
    { $state ->
        [ready] Готово к активации.
        [progress]
            Идёт дешифровка:
            { $time }
       *[required]
            Требуемое время дешифровки:
            { $time }
    }
st-nuke-ui-timing-status =
    { $active ->
        [true] Время до детонации: { $time }
       *[false] В данный момент не активна.
    }
st-nuke-ui-safety-toggle =
    { $enabled ->
        [true] Отключить защиту
       *[false] Включить защиту
    }
st-nuke-ui-command-lockout-toggle =
    { $enabled ->
        [true] Выключить блокировку
       *[false] Включить блокировку
    }
st-nuke-ui-anchor-toggle =
    { $enabled ->
        [true] Открепить
       *[false] Закрепить
    }
st-nuke-ui-decryption-toggle =
    { $enabled ->
        [true] Остановить дешифровку
       *[false] Запустить дешифровку
    }
st-nuke-ui-nuke-toggle =
    { $enabled ->
        [true] Отменить фаер-шоу
       *[false] Инициировать фаер-шоу
    }
st-nuke-window-title = Blockbuster LAFDEDE
st-nuke-decryption-complete-ui = Дешифровка завершена.
st-nuke-not-active = В данный момент не активна.
st-nuke-disable-safety = Отключить защиту
st-nuke-enable-command-lockout = Переключить блокировку
st-nuke-activate-anchor = Активировать якорь
st-nuke-start-decryption = Запустить дешифровку
st-nuke-activate = Инициировать фаер-шоу
