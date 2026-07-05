rmc-immune-to-ignition-examine = [color=cyan]{ CAPITALIZE(SUBJECT($ent)) } { $direct ->
    [true] { "нельзя поджечь" }
    *[false] { "нельзя поджечь косвенно" }
}![/color]
rmc-immune-to-fire-tile-damage-examine = [color=cyan]{ CAPITALIZE(SUBJECT($ent)) } не получает урона от огня на клетках![/color]
rmc-fire-armor-debuff-modifier-examine = [color=cyan]{ CAPITALIZE(SUBJECT($ent)) } теряет на { $percentage }% меньше брони, когда стоит в зелёном огне![/color]
