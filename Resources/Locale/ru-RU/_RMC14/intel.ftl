rmc-intel-suffix = { $baseName } ({ $number })
rmc-intel-unlocked = { $baseName } ({ $unlocked })
rmc-intel-label-name = { $baseName } { $label }
rmc-intel-label-name-parenthetical = { $baseName } ({ $label })
rmc-intel-serial-name = { $baseName } #{ $serial }
rmc-intel-serial-examine = Серийный номер — { $serial }.
rmc-intel-announcement = ТЕХНИЧЕСКИЙ ОТЧЕТ: { $points } очки доступны.
rmc-intel-announcement-gain = ТЕХНИЧЕСКИЙ ОТЧЕТ: { $points } доступны очки (+{ $change }).
rmc-intel-reports = Отчеты
rmc-intel-folders = Папки
rmc-intel-manuals = Руководства
rmc-intel-data = Data
rmc-intel-retrieve = Retrieve
rmc-intel-misc = Miscellaneous
rmc-intel-personal = Personal Clues

rmc-intel-data-disk-uploaded = { $baseName } (uploaded)
rmc-intel-color-red = [color=#eb4034]red[/color]
rmc-intel-color-black = [color=#000000]black[/color]
rmc-intel-color-blue = [color=#3449eb]blue[/color]
rmc-intel-color-yellow = [color=#ebe534]yellow[/color]
rmc-intel-color-white = [color=#ffffff]white[/color]
rmc-intel-color-grey = [color=#949494]grey[/color]
rmc-intel-color-green = [color=#5dbf36]green[/color]
rmc-intel-color-cracked-blue = [color=#3449eb]cracked blue[/color]
rmc-intel-color-bloodied-blue = [color=#3449eb]bloodied blue[/color]
rmc-intel-color-unknown = unmarked
rmc-intel-clue-found = You make out something about { $clue }.
rmc-intel-personal-clues-added = New clues have been added to your personal clues.
rmc-intel-clue-label-number = #{ $number }
rmc-intel-clue-label-serial = #{ $serial }
rmc-intel-clue-label-unmarked = no visible label
rmc-intel-clue-paper-scrap = { $intel } в { $area }.
rmc-intel-clue-progress-report = Отчет о проделанной работе в { $area }.
rmc-intel-clue-folder = { $intel } в { $area }.
rmc-intel-clue-technical-manual = { $intel } в { $area }.
rmc-intel-clue-experimental-device = Получить { $intel } в { $area }.
rmc-intel-clue-data-disk = { $color } disk [bold]{ $label }[/bold], decryption key is [bold]{ $key }[/bold] in { $area }.
rmc-intel-clue-data-terminal = Upload data from terminal [bold]{ $label }[/bold], password is [bold]{ $password }[/bold] in { $area }.
rmc-intel-clue-safe = Crack open the safe { $label }, combination lock is [bold]{ $code }[/bold] in { $area }.
rmc-intel-data-terminal-password-prompt = Enter the password
rmc-intel-data-terminal-no-power = This terminal has no power!
rmc-intel-data-terminal-no-comms = The terminal flashes a network connection error.
rmc-intel-data-terminal-wrong-password = The terminal rejects the password.
rmc-intel-data-terminal-started = You start uploading the data.
rmc-intel-data-terminal-uploading = Looks like the terminal is already uploading, better make sure nothing interrupts it!
rmc-intel-data-terminal-finished = The terminal pings softly as it finishes the upload.
rmc-intel-data-terminal-complete = There's a message on the screen that the data upload finished successfully.
rmc-intel-disk-reader-key-prompt = Enter the encryption key
rmc-intel-disk-reader-no-power = The SIGINT terminal has no power.
rmc-intel-disk-reader-occupied = There's already a disk inside the SIGINT terminal, wait for it to finish first!
rmc-intel-disk-reader-empty = The SIGINT terminal has no disk inserted.
rmc-intel-disk-reader-wrong-key = The reader buzzes, ejecting the disk.
rmc-intel-disk-reader-insert-failed = The disk cannot be inserted.
rmc-intel-disk-reader-started = You insert the disk and enter the decryption key.
rmc-intel-disk-reader-finished = The SIGINT terminal pings softly as the upload finishes and ejects the disk.
rmc-intel-disk-reader-power-lost = The SIGINT terminal powers down mid-operation as the area loses power and ejects the disk.
rmc-intel-disk-reader-disk-complete = The reader displays a message stating this disk has already been read and refuses to accept it.
rmc-intel-safe-code-prompt = Enter the safe combination.
rmc-intel-safe-wrong-code = The safe does not open.
rmc-intel-safe-complete = The safe unlocks.
rmc-intel-reading-start = You start reading the { $thing }.
rmc-intel-reading-cancelled = You get distracted and lose your train of thought, you'll have to start over reading this.
rmc-intel-reading-inactive = You don't notice anything useful. You probably need to find its instructions on a paper scrap.
rmc-intel-reading-finished = You finish reading the { $thing }.
rmc-intel-console-typing-start = You start typing in intel into the computer...
rmc-intel-console-typing-no-new = You start typing in intel into the computer... and you have nothing new to add...
rmc-intel-console-typing-cancelled = You get distracted and lose your train of thought, you'll have to start the typing over...
rmc-intel-console-submit-no-new = ...and you have nothing new to add...
rmc-intel-console-submit-done = ...and done! You uploaded { $amount } entries!
rmc-intel-survivor-pickup = Вам не нужна эта { $thing }.
    Сначала сосредоточьтесь на том, чтобы выбраться живым.
rmc-intel-survivor-read = Вам не нужно читать { $thing }.
    Сосредоточьтесь на том, чтобы сначала выбраться живым.
rmc-intel-survivor-xeno-pull = Попытка забрать с собой { $thing } только замедлит меня.
    Мне следует сначала сосредоточиться на получении помощи.
rmc-intel-survivor-corpse-pull = Я не могу сохранить { $thing }, это только замедлит меня.
    Мне следует сначала сосредоточиться на получении помощи.


## Intel Objectives Window
rmc-ui-intel-title = Цели в технологическом древе морской пехоты
rmc-ui-intel-header = [bold]Цели в технологическом древе морской пехоты[/bold]
rmc-ui-intel-tech-points = [bold]Очки технологий[/bold]
rmc-ui-intel-tier = [bold]Уровень[/bold]
rmc-ui-intel-objectives = [bold]Цели[/bold]
rmc-ui-intel-documents = [color=#5B88B0]Документы:[/color]
rmc-ui-intel-upload-data = [color=#5B88B0]Загрузить данные:[/color]
rmc-ui-intel-retrieve-items = [color=#5B88B0]Извлечь предметы:[/color]
rmc-ui-intel-miscellaneous = [color=#5B88B0]Разное:[/color]
rmc-ui-intel-analyze-chemicals = [color=#5B88B0]Анализировать химикаты:[/color]
rmc-ui-intel-rescue-survivors = [color=#5B88B0]Спасти выживших:[/color]
rmc-ui-intel-recover-corpses = [color=#5B88B0]Забрать тела:[/color]
rmc-ui-intel-colony-comms = [color=#5B88B0]Связь колонии:[/color]
rmc-ui-intel-colony-power = [color=#5B88B0]Энергия колонии:[/color]
rmc-ui-intel-clues = [bold]Подсказки[/bold]
rmc-ui-intel-points-value = { $value }
rmc-ui-intel-tier-value = { $value }
rmc-ui-intel-total-credits = Всего заработано кредитов: { $value }
rmc-ui-intel-progress = { $current } / { $total }
rmc-ui-intel-infinite-progress = { $current } / ∞
rmc-ui-intel-colony-status =
    { $online ->
        [true] Онлайн.
        *[false] Офлайн.
    }

## Tech Control Console
rmc-ui-tech-tier-header = [font size=14][bold]Уровень: { $tier }[/bold][/font]
rmc-ui-tech-points = [font size=14][bold]Очки: { $points }[/bold][/font]
rmc-ui-tech-points-value = Очки технологий: { $value }
rmc-ui-tech-repurchasable = Можно выкупить
rmc-ui-tech-incremental-price = Увеличивающаяся цена: +{ $increase } за покупку
rmc-ui-tech-purchase-button = Купить

rmc-ui-tech-information-header = [bold]Информация[/bold]
rmc-ui-tech-name-label = [color=#5B88B0]Название:[/color]
rmc-ui-tech-description-label = [color=#5B88B0]Описание:[/color]
rmc-ui-tech-cost-label = [color=#5B88B0]Стоимость:[/color]
rmc-ui-tech-statistics-label = [color=#5B88B0]Статистика:[/color]

## Tech Tree Options
rmc-intel-tech-unlock-tier-1-name = Unlock Tier 1
rmc-intel-tech-unlock-tier-2-name = Unlock Tier 2
rmc-intel-tech-unlock-tier-3-name = Unlock Tier 3
rmc-intel-tech-unlock-tier-4-name = Unlock Tier 4
rmc-intel-tech-unlock-tier-description = Transitions the tree to another tier.
rmc-intel-tech-requisition-arc-name = Humvee ARC
rmc-intel-tech-requisition-arc-description = Unlocks the ARC-configured humvee for vehicle supply.
rmc-intel-tech-requisition-budget-name = Requisition Budget Increase
rmc-intel-tech-requisition-budget-description = Distributes resources to requisitions for spending.
rmc-intel-tech-dropship-budget-name = Dropship Budget Increase
rmc-intel-tech-dropship-budget-description = Distributes resources to the dropship fabricator.
rmc-intel-tech-ob-he-name = Additional OB projectiles — HE
rmc-intel-tech-ob-he-description = Highly explosive bombardment ammo, to be loaded into the orbital cannon.
rmc-intel-tech-ob-incendiary-name = Additional OB projectiles — Incendiary
rmc-intel-tech-ob-incendiary-description = Highly flammable bombardment ammo, to be loaded into the orbital cannon.
rmc-intel-tech-ob-cluster-name = Additional OB projectiles — Cluster
rmc-intel-tech-ob-cluster-description = Highly explosive bombardment ammo that fragments, to be loaded into the orbital cannon.
rmc-intel-tech-wake-troops-name = Wake Up Additional Troops
rmc-intel-tech-wake-troops-description = Wakes up additional troops to fight against any threats.
rmc-intel-tech-wake-specialist-name = Wake Up Additional Specialist
rmc-intel-tech-wake-specialist-description = Wakes up an additional specialist to fight against any threats.
rmc-intel-tech-nuclear-device-name = Nuclear Device
rmc-intel-tech-nuclear-device-description = Purchase a nuclear device. Only purchasable 116 minutes into the operation. It's the only way to be sure.

## Tech Tree Announcements
rmc-intel-tech-announcement-special-assets-author = ALMAYER SPECIAL ASSETS AUTHORIZED
rmc-intel-tech-announcement-defcon-author = ALMAYER DEFCON LEVEL INCREASED
rmc-intel-tech-announcement-arc = ARC deployment has been authorised for this operation.
rmc-intel-tech-announcement-requisition-budget = Additional supply budget has been authorised for this operation.
rmc-intel-tech-announcement-dropship-budget = Additional dropship part fabricator points have been authorised for this operation.
rmc-intel-tech-announcement-tier-2 = THREAT ASSESSMENT LEVEL INCREASED TO LEVEL 2. LEVEL 2 assets have been authorised to handle the situation.
rmc-intel-tech-announcement-ob-he = Additional Orbital Bombardment warheads (HE) have been delivered to Requisitions' ASRS.
rmc-intel-tech-announcement-ob-incendiary = Additional Orbital Bombardment warheads (Incendiary) have been delivered to Requisitions' ASRS.
rmc-intel-tech-announcement-ob-cluster = Additional Orbital Bombardment warheads (Cluster) have been delivered to Requisitions' ASRS.
rmc-intel-tech-announcement-tier-3 = THREAT ASSESSMENT LEVEL INCREASED TO LEVEL 3. LEVEL 3 assets have been authorised to handle the situation.
rmc-intel-tech-announcement-wake-troops = Additional troops are being taken out of cryo.
rmc-intel-tech-announcement-wake-specialist = An additional specialist is being taken out of cryo.
rmc-intel-tech-announcement-tier-4 = THREAT ASSESSMENT LEVEL INCREASED TO LEVEL 4. LEVEL 4 assets have been authorised to handle the situation.
rmc-intel-tech-announcement-nuclear-device = The deployment of Nuclear Ordnance has been authorized and will be delivered to the Requisitions Department via ASRS.
