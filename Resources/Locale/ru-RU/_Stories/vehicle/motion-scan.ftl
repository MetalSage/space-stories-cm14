st-motion-detector-scan-disabled = { CAPITALIZE($md) } должен быть активирован, чтобы просканировать { $target }.

st-motion-detector-scan-start-self = Вы начинаете перенастраивать { CAPITALIZE($md) }, чтобы просканировать внутренности { $target } на наличие сигнатур.
st-motion-detector-scan-start-others = { $user } возится с { CAPITALIZE($md) }, направляя его на { $target }.

st-motion-detector-scan-stop-self = Вы прекращаете попытку сканировать внутренности { $target }.
st-motion-detector-scan-stop-others = { $user } перестаёт возиться с { CAPITALIZE($md) }.

st-motion-detector-scan-finish-self = Вы заканчиваете перенастройку { CAPITALIZE($md) } и сканирование { $target } на наличие сигнатур.
st-motion-detector-scan-finish-others = { $user } заканчивает возиться с { CAPITALIZE($md) }.

st-motion-detector-scan-result =
    { CAPITALIZE($md) } показывает
    { $humans ->
        [0] ни одной сигнатуры
       *[other] примерно { $humans } сигнатур
    }
    { $xenos ->
        [0] и ни одной аномальной
       *[other] и около { $xenos } аномальных сигнатур
    } внутри { $target }.

st-motion-detector-scan-empty = { CAPITALIZE($md) } не улавливает никаких сигнатур — похоже, транспорт пуст. В теории.
