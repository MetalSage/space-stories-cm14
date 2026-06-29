rmc-medical-examine-unrevivable = [color=purple][italic]{ CAPITALIZE(POSS-ADJ($victim)) } глаза потухли, нет признаков жизни.[/italic][/color]

rmc-medical-examine-headless = [color=purple][italic]{ CAPITALIZE(SUBJECT($victim)) } определенно мёртв.[/italic][/color]

rmc-medical-examine-unconscious = [color=lightblue]{ CAPITALIZE(SUBJECT($victim)) } { GENDER($victim) ->
    [epicene] кажется
    *[other] кажется
} без сознания.[/color]

rmc-medical-examine-dead = [color=red]{ CAPITALIZE(SUBJECT($victim)) } не дышит.[/color]

rmc-medical-examine-dead-simple-mob = [color=red]{ CAPITALIZE(SUBJECT($victim)) } мёртво. Сыграл в ящик.[/color]

rmc-medical-examine-dead-xeno = [color=red]{ CAPITALIZE(SUBJECT($victim)) } мёртво. Сыграл в ящик. Отправился в тот великий улей в небесах.[/color]

rmc-medical-examine-alive = [color=green]{ CAPITALIZE(SUBJECT($victim)) } жив и дышит.[/color]

rmc-medical-examine-bleeding = [color=#d10a0a]{ CAPITALIZE(SUBJECT($victim)) } { CONJUGATE-HAVE($victim) } кровоточащие раны на { POSS-ADJ($victim) } теле.[/color]

rmc-medical-examine-verb = Показать медицинские действия
