st-ui-vehicle-status-title = Статус транспорта

st-ui-vehicle-hull-integrity = Целостность корпуса: { $integrity }%
st-ui-vehicle-hull-destroyed = Корпус уничтожен

st-ui-vehicle-door-state-label = Двери
st-ui-vehicle-door-state =
    { $locked ->
        [true] Заблокированы
       *[false] Разблокированы
    }

st-ui-vehicle-armor-resistances =
    { $unfolded ->
        [true] ↑ Сопротивления брони
       *[false] ↓ Сопротивления брони
    }

st-ui-vehicle-resistance-entry =
    { $type ->
        [Heat] Биологическая защита: 
        [Slash] Защита от порезов: 
        [Piercing] Баллистическая защита: 
        [Blunt] Защита от ударов: 
        [Expl] Взрывоустойчивость: 
       *[other] { $type }: 
    }

st-ui-vehicle-passengers =
    { $unfolded ->
        [true] ↑ Пассажиры
       *[false] ↓ Пассажиры
    }

st-ui-vehicle-total-passengers = Пассажиров: 

st-ui-vehicle-passengers-category = Живые: 
st-ui-vehicle-dead-category = Раненые: 
st-ui-vehicle-xeno-category = Ксеноморфы: 
st-ui-vehicle-role-slots = { $name }: 

st-ui-vehicle-hardpoints = Узлы вооружения
st-ui-vehicle-no-hardpoints = Нет установленных узлов
st-ui-vehicle-hardpoint-integrity = Целостность: { $integrity }%
st-ui-vehicle-hardpoint-destroyed = Уничтожено

st-ui-vehicle-ammo = Боеприпасы: { $current } / { $max }
st-ui-vehicle-mags = Магазины: { $current } / { $max }

st-ui-select-hardpoint-title = Выбрать точку крепления

st-ui-select-hardpoint-contain = Доступные точки крепления:

st-vehicle-ui-no-any-hardpoint = Отсутствуют доступные точки крепления.

st-vehicle-ui-magazine-loaded = ✓
st-vehicle-ui-magazine-empty = ✗
st-vehicle-ui-ammo-info = | Боеприпасы: { $current }/{ $max }
st-vehicle-ui-hardpoint-button = { $name } [{ $status }]{ $ammo }
st-vehicle-ui-spare-info = Запасные магазины: { $current }/{ $max }
st-vehicle-ui-available-weapons = Доступное оружие:
st-vehicle-ui-loaded-empty-legend = [✓] = Загруженный магазин | [✗] = Пустой
st-vehicle-ui-spare-legend = (Запас/Макс) показывает количество запасных магазинов
st-vehicle-ui-click-to-reload = Нажмите на оружие для перезарядки из запасных магазинов
st-vehicle-ui-window-title = Загрузчик боекомплекта