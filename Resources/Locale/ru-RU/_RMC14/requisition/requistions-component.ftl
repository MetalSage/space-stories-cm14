# Requisition Computer
requisition-paperwork-receiver-name = Отдел логистики
requisition-paperwork-reward-message = Подтверждение получено! Переведено ${ $amount } из излишков бюджета.

# Requisition Invoice
requisition-paper-print-name = { $name } счет-фактура
requisition-paper-print-manifest = { "[" }head=2]
    { $containerName }[/head][bold]{ $content }[/bold][head=2]
    Вес: { $weight } фунтов
    Партия: { $lot }
    Серийный номер: { $serialNumber }[/head]
requisition-paper-print-content = - { $count } { $item }

# Supply Drop Console
ui-supply-drop-consle-name = Консоль вызова припасов
ui-supply-drop-console-name-bolded = [bold]ВЫЗОВ ПРИПАСОВ[/bold]
ui-supply-drop-console-longitude = Долгота:
ui-supply-drop-console-latitude = Широта:
ui-supply-drop-pad-status = [bold]Состояние площадки снабжения[/bold]
ui-supply-drop-console-update = Обновить
ui-supply-drop-console-ready = Готово к запуску!
ui-supply-drop-console-launch = ЗАПУСТИТЬ ВЫЗОВ ПРИПАСОВ
ui-supply-drop-console-launch-confirmation = Вы уверены, что хотите отправить ящик?
ui-supply-drop-console-cooldown = { $time } секунд до следующего запуска
ui-supply-drop-crate-status =
    { $hasCrate ->
        [true] Состояние площадки снабжения: контейнер загружен.
        *[false] Контейнер не загружен.
    }