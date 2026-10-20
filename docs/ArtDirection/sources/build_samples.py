"""Rebuild editable art-review diagrams and offline UI samples; Python stdlib only."""
from pathlib import Path
from html import escape
import json
import shutil

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
FONT = REPO / 'Assets/TextMesh Pro/Fonts/LiberationSans.ttf'
TYPE = ROOT / 'selected/typography'
shutil.copyfile(FONT, TYPE / 'LiberationSans.ttf')
shutil.copyfile(FONT.with_name('LiberationSans - OFL.txt'), TYPE / 'OFL.txt')
P = dict(ink='#202E37', paper='#EEE8D9', muted='#59636A', blue='#365E73',
         ochre='#B98335', green='#315E48', red='#963E35', wood='#A98255', line='#B7B2A6')

def rect(x,y,w,h,c,stroke=None):
    return f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{c}"'+(f' stroke="{stroke}" stroke-width="2"' if stroke else '')+'/>'
def text(x,y,s,size=22,c=None,weight=400):
    return f'<text x="{x}" y="{y}" font-size="{size}" font-weight="{weight}" fill="{c or P["ink"]}">{escape(str(s))}</text>'
def lines(x,y,items,size=22,c=None,gap=34):
    return ''.join(text(x,y+i*gap,s,size,c) for i,s in enumerate(items))
def pair(x,y,right,label,value,size=25):
    return text(x,y,label,size)+f'<text x="{right}" y="{y}" text-anchor="end" font-size="{size}" fill="{P["ink"]}">{escape(value)}</text>'
def line(x1,y1,x2,y2,c=None):
    return f'<path d="M{x1} {y1}H{x2}" stroke="{c or P["line"]}"/>' if y1==y2 else f'<path d="M{x1} {y1}L{x2} {y2}" stroke="{c or P["line"]}" stroke-width="2"/>'
def svg(body,w=1600,h=900,title='Hustle & Scale — design mockup'):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" role="img"><title>{escape(title)}</title><g font-family="Liberation Sans,Arial,sans-serif">{body}</g></svg>'
def lamp(x,y,s=1,back=False):
    b=rect(10,20,80,88,P['blue'])+rect(23,0,54,25,P['ink'])+rect(31,7,38,14,P['paper'])
    b+=rect(18,28,64,69,P['ink'])+rect(24,34,52,56,'#D8D5BA') if not back else rect(22,40,56,52,P['ink'])
    b+=rect(3,102,34,14,P['ink'])+rect(63,102,34,14,P['ink'])
    return f'<g transform="translate({x} {y}) scale({s})">{b}</g>'
def world():
    b=rect(0,0,1600,900,'#B9B8AF')+rect(0,555,1600,345,'#777B78')
    b+=rect(1170,90,325,525,'#455057')+rect(1190,110,280,445,'#A3B6BE')
    b+=rect(1210,280,240,280,'#A88C73')+rect(1240,330,75,115,'#49545A')
    b+=rect(80,130,480,28,P['ink'])+rect(80,300,480,22,P['ink'])+rect(80,460,480,22,P['ink'])
    b+=rect(90,130,16,430,P['ink'])+rect(530,130,16,430,P['ink'])
    b+=rect(140,222,135,78,'#A98857')+rect(350,375,130,85,'#8F785B')
    b+='<path d="M0 620L1120 500L1600 670L480 860Z" fill="#A98255"/>'
    b+=rect(200,752,30,148,P['ink'])+rect(1430,700,24,200,P['ink'])
    b+=rect(80,555,300,98,'#8C704D')+rect(95,565,270,12,'#BA9867')
    for x in [110,202,294]: b+=line(x,570,x,650,'#5D4F3F')
    b+=lamp(570,455,1.2)+lamp(745,435,1.2)+lamp(920,415,1.2)
    b+=rect(1120,540,120,75,P['ink'])+rect(1133,550,90,28,P['paper'])
    b+=rect(1330,385,58,138,'#566044')+rect(1333,520,20,95,P['ink'])+rect(1365,520,20,95,P['ink'])
    b+='<ellipse cx="1360" cy="354" rx="27" ry="40" fill="#B29366"/>'
    b+=rect(1336,315,49,14,'#3C352F')+rect(1320,388,10,120,'#B29366')+rect(1388,388,10,120,'#B29366')
    b+='<circle cx="1350" cy="348" r="5" fill="white"/><circle cx="1372" cy="348" r="5" fill="white"/><circle cx="1351" cy="348" r="2"/><circle cx="1373" cy="348" r="2"/>'
    b+=line(1355,372,1367,372,P['ink'])
    return b
def button(x,y,w,label,active=True):
    return rect(x,y,w,54,P['ink'] if active else '#C7C3B8')+text(x+18,y+35,label,22,P['paper'] if active else P['muted'],700)
def header(title,sub='ГОРОД · ВИЗУАЛЬНЫЙ МАКЕТ · МЕНЮ ОСТАНАВЛИВАЕТ ВРЕМЯ'):
    return rect(180,115,1240,675,P['paper'])+rect(180,115,12,675,P['blue'])+text(220,163,title,34,weight=700)+text(220,200,sub,17,P['muted'])+line(220,220,1380,220)
def foot(note='Esc — назад · Tab / стрелки — фокус · Enter — действие'):
    return text(220,760,note,19,P['muted'])
def state_row(y,no,status,place,mark='?'):
    return rect(220,y-29,720,70,'#E1DCCD')+text(238,y+12,mark,28,weight=700)+text(285,y+3,f'Фонарь {no} · {status}',24,weight=700)+text(285,y+28,place,18,P['muted'])

views={}
def add(key,title,b):
    views[key]={'title':title,'svg':svg(b+text(24,880,'HUSTLE & SCALE · КОНЦЕПТ UI / НЕ КАДР UNITY',16,'#FFFFFF'))}

base=world()
b=base+rect(28,28,250,80,P['ink'])+text(48,61,'НАЛИЧНЫЕ',16,P['paper'])+text(48,94,'$120',30,P['paper'],700)
b+=rect(1160,28,412,112,P['ink'])+lines(1180,60,['A · 2 исправных · $260','Принят · осталось 07:32'],22,P['paper'])
b+=text(796,455,'·',30,'#FFFFFF')+rect(600,630,400,58,P['ink'])+text(620,667,'E  Проверить фонарь 02',24,P['paper'])
for i in range(3): b+=rect(625+i*118,780,106,65,P['ink'],P['paper'] if i==1 else '#59636A')+text(644+i*118,820,f'{i+1}   ?',25,P['paper'])
add('hud','HUD',b)
b=base+rect(0,0,570,900,P['ink'])+text(64,150,'HUSTLE',72,P['paper'],700)+text(64,227,'& SCALE',72,P['paper'],700)+text(64,280,'МАЛОЕ ДЕЛО. СВОИМИ РУКАМИ.',19,P['paper'])
for i,s in enumerate(['Продолжить','Новая игра','Настройки','Выйти']): b+=button(64,380+i*76,390,s)
add('menu','Главное меню',b)
b=base+header('Имущество')
b+=lines(220,262,['Три физических экземпляра. Место хранения имеет значение.'],22)
for i,(s,p,m) in enumerate([('проверен · исправен','Ящик у мастерской','✓'),('проверен · дефект батареи','На столе','!'),('не проверен','Карман','?')]): b+=state_row(325+i*105,i+1,s,p,m)
b+=lines(1000,300,['Наличные   $120','Батареи         0','В руках: ничего','На столе: фонарь 02'],22,gap=45)+button(1000,555,340,'Осмотреть выбранный')+foot()
add('inventory','Инвентарь',b)
b=base+header('Фонарь 02 · карточка экземпляра')+lamp(340,315,2.3,True)
b+=lines(760,290,['ПЕРЕНОСНОЙ РАБОЧИЙ ФОНАРЬ','Состояние: не проверен','Местоположение: стол','Косметические следы не равны диагнозу','Покупателю нужна проверка на столе.'],24,gap=58)
b+=button(760,620,450,'Вернуться к столу')+foot()
add('item','Карточка товара',b)
b=base+header('Проверка · фонарь 02')+lamp(335,320,2.5,True)
b+=rect(280,650,365,44,P['ink'])+text(300,680,'Батарейный отсек · осмотр',20,P['paper'])
b+=lines(780,280,['!  Дефект аккумулятора','Короткая вспышка → свет погас','Результат подтверждён тестом.','Замена требует своей батареи.','Ремонт: +04:00 к игровому времени.','После замены нужен повторный тест.'],23,gap=48)
b+=button(780,625,545,'Заменить · нет батареи ($50)',False)+foot()
add('inspection','Осмотр и диагностика',b)
b=base+header('Продавец фонарей · покупка партии')
for i in range(3): b+=lamp(270+i*170,315,1.15)
b+=lines(840,280,['3 подержанных рабочих фонаря','Диагностика: не выполнена'],25,gap=51)
for i,(label,value) in enumerate([('Стоимость партии','$180'),('Наличные до','$300'),('Наличные после','$120')]):
    b+=pair(840,382+i*51,1360,label,value)
b+=text(840,535,'Партия оплачивается один раз.',25)
b+=button(840,638,360,'Купить партию · $180')+button(1220,638,160,'Уйти')+foot()
add('purchase','Покупка',b)
b=base+header('Передача · покупатель A')+lines(220,280,['Заказ принят · осталось 07:32','Требуется 2 проверенных исправных.'],24,gap=42)
b+=state_row(410,'01','исправен','Предъявлен в переносимом ящике','✓')+state_row(515,'03','исправен','Предъявлен в переносимом ящике','✓')
for i,(label,value) in enumerate([('Выплата','$260'),('До','$120'),('После','$380')]):
    b+=pair(1000,300+i*49,1360,label,value,24)
b+=lines(1000,447,['Передача целиком.','Остаток: фонарь 02.'],24,gap=49)+button(1000,625,360,'Передать 2 · получить $260')+foot()
add('sale','Продажа',b)
b=base+header('Покупатель Б · условия предложения')+lines(220,280,['«Нужен один исправный рабочий фонарь.»','Количество                                             1','Требование                 проверен и исправен','Оплата                                               $125','Срок                    18 минут от принятия','До принятия обязательства таймер не идёт.'],25,gap=55)
b+=button(220,638,400,'Принять обязательство')+button(650,638,350,'Уйти без принятия')+foot()
add('dialogue','Диалог / предложение NPC',b)
b=base+header('Деньги и остатки')
for i,(label,value) in enumerate([('Начальные деньги','$300'),('Расходы: партия','−$180'),('Выручка: покупатель A','+$260'),('Наличные сейчас','$380'),('Остаток: 1 неисправный фонарь','не деньги'),('Денежный результат','+$80')]):
    b+=pair(220,285+i*60,1360,label,value,26)
b+=foot('Нет автоматического финала · продолжение в общем городе')
add('finance','Финансовый обзор',b)
b=base+header('История операций')
for i,row in enumerate(['01    Покупка партии · 3 шт.                  −$180       $120','02    Проверка 01 · исправен                       $0       $120','03    Проверка 02 · дефект батареи            $0       $120','04    Проверка 03 · исправен                       $0       $120','05    Передача A · 01 + 03                      +$260       $380']):
    b+=text(220,292+i*69,row,24)+line(220,313+i*69,1380,313+i*69)
b+=foot('Номер операции и экземпляры связывают оплату с передачей.')
add('history','История операций',b)
b=base+header('Организация рабочего места','БУДУЩЕЕ · ПРЕЗЕНТАЦИЯ НАПРАВЛЕНИЯ, НЕ РЕАЛИЗОВАННАЯ ПОКУПКА')
b+=lines(220,285,['Сейчас: один стол, ящик и временное хранение.','Следующий визуальный уровень: понятные зоны хранения,','отдельная зона упаковки и рабочий свет.','Условия открытия и цена ещё не определены.','Первая продажа сама по себе не меняет помещение.'],25,gap=64)
b+=button(220,640,590,'Покупка улучшения не реализована',False)+foot()
add('growth','Развитие бизнеса',b)
b=base+header('Настройки','МАКЕТ ИНФОРМАЦИОННОЙ АРХИТЕКТУРЫ · ЗНАЧЕНИЯ НЕ МЕНЯЮТ ИГРУ')
b+=lines(220,285,['Размер текста                                  100% / 125% / 150%','Уменьшение движения                         включено','Язык                                                   русский','Громкость действий                              80%','Поле зрения                                        текущее, без изменения','Управление                                         показать привязки'],25,gap=58)
b+=button(220,645,350,'Назад')+foot()
add('settings','Настройки',b)
for key,v in views.items(): (ROOT/f'selected/interface/{key}.svg').write_text(v['svg'],encoding='utf-8')

# An exact palette/type/graphic sheet; generated concept lettering is not authoritative.
b=rect(0,0,1600,900,P['paper'])+text(60,84,'ГРАФИКА РАБОЧЕГО МЕСТА',42,weight=700)
for i,(name,c) in enumerate(P.items()):
    x=60+i%5*305; y=140+i//5*150
    b+=rect(x,y,270,70,c)+text(x,y+100,f'{name}  {c}',22)
b+=text(60,520,'Фонарь №02 · проверить аккумулятор',46,weight=700)+text(60,573,'Ёжик, съём, Йй, Ъъ, Ыы, Ьь · $300 → $120 · 0123456789',28)
b+=lines(60,645,['Liberation Sans Regular · основной текст 24 / 32','Иерархия: 40 заголовок / 28 раздел / 24 действие / 20 подпись','?  Не проверен      ✓  Исправен      !  Дефект      ↻  Нужен повторный тест'],24,gap=48)
b+=rect(1050,525,420,230,P['ink'])+text(1075,570,'ВЕРТА / VERTA',35,P['paper'],700)+lines(1075,620,['РАБОЧИЙ СВЕТ','ЭКЗЕМПЛЯР 02','ПРОВЕРКА: НЕ ВЫПОЛНЕНА'],22,P['paper'])
(TYPE/'type-palette-signage.svg').write_text(svg(b),encoding='utf-8')

# Editable pictograms use paths, independent of the font's special-symbol coverage.
icons={
    'Не проверен':'<path d="M8 7C8 1 18 1 18 7C18 11 12 10 12 16M12 20V22"/>',
    'Исправен':'<path d="M2 12L9 19L23 3"/>',
    'Дефект':'<path d="M12 2L23 22H1ZM12 8V15M12 18V20"/>',
    'Повторный тест':'<path d="M21 10A9 9 0 1 0 19 19M21 2V10H13"/>',
    'Батарея':'<path d="M4 5H20V23H4ZM9 1H15V5M8 11H16M12 7V15M8 19H16"/>',
    'Ящик':'<path d="M2 7L12 2L22 7V22H2ZM2 7H22M12 7V22M6 12H9M15 12H18"/>',
    'Фонарь':'<path d="M3 7H21V21H3ZM7 7V2H17V7M7 11H17V17H7M1 23H8M16 23H23"/>',
    'Документ':'<path d="M4 1H16L22 7V23H4ZM16 1V7H22M8 12H18M8 16H18M8 20H15"/>'
}
b=rect(0,0,1600,900,P['paper'])+text(60,80,'ПИКТОГРАММЫ · 24 ЕДИНИЦЫ / ЛИНИЯ 2',36,weight=700)
for i,(name,path) in enumerate(icons.items()):
    x=70+i%4*390; y=180+i//4*340
    b+=f'<g transform="translate({x+65} {y}) scale(5)" fill="none" stroke="{P["ink"]}" stroke-width="2" stroke-linejoin="miter" stroke-linecap="square">{path}</g>'
    b+=text(x,y+185,name,25,weight=700)
b+=text(60,830,'В мире: бирка / коробка / прибор. В UI: тот же знак + текст состояния.',24)
(TYPE/'iconography.svg').write_text(svg(b),encoding='utf-8')

b=rect(0,0,1600,900,P['paper'])+text(60,80,'СТОЛ · РАБОЧАЯ ГИПОТЕЗА ДЛЯ ПРОВЕРКИ В UNITY',34,weight=700)
b+=rect(80,180,1440,380,'#A98255',P['ink'])
for x,w,label,sub in [(100,370,'ПРИЁМ','Ящик слева / вне зоны луча'),(490,590,'ПРОВЕРКА','Один фонарь / свободная поверхность'),(1100,400,'ПОДГОТОВКА','Расходники и будущая упаковка')]:
    b+=rect(x,210,w,270,P['paper'])+text(x+20,260,label,28,weight=700)+text(x+20,305,sub,18)
b+=lamp(700,335,1)+rect(750,650,110,70,P['ink'])+text(80,855,'Камера 1,65 м · FOV 70° город / 75° sandbox',24)
b+=line(805,650,750,465,P['red'])+lines(80,635,['Факты: E ≤ 2,5 м, ближайший коллайдер блокирует луч.','Фонарь города: 0,30 × 0,48 × 0,26 м (бокс).','Рекомендация: проверять с 0,75–1,10 м, без автозума.','Декор не пересекает луч, хват, проход и зону установки.'],24,gap=45)
(ROOT/'selected/environment/spatial-rules.svg').write_text(svg(b),encoding='utf-8')
b=rect(0,0,1600,900,P['paper'])+text(60,80,'СОСТОЯНИЯ · ЗНАНИЕ ИГРОКА ОТДЕЛЕНО ОТ ВЕЩИ',36,weight=700)
for i,(t,d,m) in enumerate([('НЕ ПРОВЕРЕН','Одинаковый корпус','?'),('ПРОВЕРЕН','Устойчивый свет','✓'),('ДЕФЕКТ БАТАРЕИ','Кратко загорелся и погас','!'),('ПОСЛЕ РЕМОНТА','Повторить проверку','↻')]):
    x=60+i*390; b+=lamp(x+95,220,1.4)+text(x+10,470,m,46,weight=700)+text(x+10,530,t,24,weight=700)+text(x+10,572,d,20)
b+=lines(60,695,['Косметический износ не раскрывает диагноз. Включение F не заменяет тест.','Звук + подпись подтверждают результат; красный и зелёный не единственные признаки.','0,12–0,25 с на смену представления; длительность теста — параметр текущей механики.'],25,gap=52)
(ROOT/'selected/interactions/states.svg').write_text(svg(b),encoding='utf-8')

# Offline review tool. The interactive fixture demonstrates knowledge states and one-time feedback,
# and intentionally does not implement or persist a second economy.
css='''@font-face{font-family:Review;src:url(selected/typography/LiberationSans.ttf)}*{box-sizing:border-box}body{margin:0;background:#202e37;color:#eee8d9;font:18px Review,Arial,sans-serif}header,nav,.controls,footer{padding:18px 3vw}h1{font-size:28px;margin:0 0 10px}p{margin:8px 0;line-height:1.45}a{color:inherit}nav{display:flex;flex-wrap:wrap;gap:8px;border-block:1px solid #59636a}button,select{font:inherit;border:1px solid #b7b2a6;border-radius:0;padding:10px 14px;background:#eee8d9;color:#202e37}button:focus-visible,select:focus-visible{outline:3px solid #b98335;outline-offset:3px}button[aria-pressed=true]{background:#b98335}button:disabled{background:#b7b2a6;color:#59636a}.viewport{width:94vw;max-width:1600px;margin:24px auto;aspect-ratio:16/9;background:#777b78;display:flex;align-items:center;justify-content:center}.viewport svg{width:100%;height:100%;max-height:100%;font-family:Review,Arial,sans-serif}.controls{display:flex;gap:12px;flex-wrap:wrap;align-items:center}.status{min-height:32px;margin-left:3vw}.gallery{display:grid;grid-template-columns:repeat(auto-fit,minmax(310px,1fr));gap:20px;margin:30px 3vw}.gallery img{width:100%;display:block}.gallery figure{margin:0}.gallery figcaption{line-height:1.5;padding:10px 0}footer{border-top:1px solid #59636a}body[data-ratio="16/10"] .viewport{aspect-ratio:16/10}body[data-ratio="21/9"] .viewport{aspect-ratio:21/9}body[data-ratio="21/9"] .viewport svg{width:auto;max-width:100%}@media(max-width:700px){body{font-size:16px}.viewport{width:100vw}header,nav,.controls{padding:14px}h1{font-size:23px}}@media(prefers-reduced-motion:reduce){*{animation:none!important;transition:none!important}}'''
html='''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Hustle & Scale — художественная приёмка</title><style>'''+css+'''</style><body data-ratio="16/9"><header><h1>HUSTLE & SCALE / простая игровая стилизация</h1><p>Рабочая рекомендация после просмотра Schedule I. Стиль ещё не утверждён владельцем. Все изображения ниже — концепты, кроме подписанного исходного кадра Unity.</p><p><a href="HUSTLE_AND_SCALE_ART_DIRECTION_v1.0.md">Art Direction Bible</a> · <a href="VISUAL_EVALUATION.md">Сравнение</a> · <a href="PRODUCTION_GUIDELINES.md">Производство и проверки</a></p></header><nav aria-label="Экраны"></nav><section class="controls"><label>Формат <select id="ratio"><option>16/9</option><option>16/10</option><option>21/9</option></select></label><button id="previous">Предыдущая ситуация</button><button id="next">Следующая ситуация</button><button id="test">Показать результат проверки</button><button id="settle">Показать получение оплаты</button><button id="reset">Сбросить демонстрацию</button></section><p class="status" role="status" aria-live="polite">Демонстрационные состояния; с сохранением и экономикой игры не связаны.</p><main><div class="viewport" aria-label="Визуальный макет выбранного экрана"></div><section class="gallery" aria-label="Визуальные образцы"><figure><img src="selected/gameplay/workshop-concept.png" alt="Рекомендуемая простая игровая стилизация"><figcaption>B / Простая городская 3D-стилизация. Сгенерированный концепт. UI в растровом изображении не нормативен.</figcaption></figure><figure><img src="exploration/direction-A/concept-rejected.png" alt="Отклонённый реализм"><figcaption>A / Реализм — отклонён владельцем.</figcaption></figure><figure><img src="exploration/direction-C/concept.png" alt="Графическая альтернатива с контурами"><figcaption>C / Районная печать. Контуры и двухтоновый свет.</figcaption></figure><figure><img src="exploration/direction-D/concept.png" alt="Технический альбом"><figcaption>D / Технический альбом. Прямые сборные формы.</figcaption></figure><figure><img src="selected/objects/asset-sheet.png" alt="Фонарь, материалы и персонажи"><figcaption>Производственный образец; не готовые 3D-ассеты.</figcaption></figure><figure><img src="selected/environment/progression.png" alt="Начальная и организованная мастерская"><figcaption>Рост организации — концепт будущего, без цены и автоматической покупки.</figcaption></figure><figure><img src="exploration/comparison/unity-baseline.png" alt="Настоящий исходный кадр Unity"><figcaption>Реальный исходный кадр камеры открытой DevelopmentSandbox в Edit Mode. Не Play Mode и не новый стиль.</figcaption></figure></section></main><footer>Макеты SVG редактируемы. Сценарий: городская версия FIRST DEAL; нет RepeatB и завершения смены. Рост и история операций — проектирование будущих экранов.</footer><script>const VIEWS='''+json.dumps(views,ensure_ascii=False)+''';const nav=document.querySelector('nav'),viewport=document.querySelector('.viewport'),status=document.querySelector('.status');let current='hud',tested=false,paid=false,step=0;const sequence=['hud','purchase','inspection','sale','finance'];function show(key){current=key;viewport.innerHTML=VIEWS[key].svg;viewport.setAttribute('aria-label',VIEWS[key].title);nav.querySelectorAll('button').forEach(b=>b.setAttribute('aria-pressed',b.dataset.key===key?'true':'false'));}Object.entries(VIEWS).forEach(([key,v])=>{const b=document.createElement('button');b.textContent=v.title;b.dataset.key=key;b.addEventListener('click',()=>show(key));nav.append(b)});document.querySelector('#ratio').onchange=e=>document.body.dataset.ratio=e.target.value;document.querySelector('#next').onclick=()=>{step=Math.min(4,step+1);show(sequence[step])};document.querySelector('#previous').onclick=()=>{step=Math.max(0,step-1);show(sequence[step])};document.querySelector('#test').onclick=()=>{tested=true;show('inspection');status.textContent='Демонстрация: фонарь 02 — дефект аккумулятора. До теста состояние неизвестно.';};document.querySelector('#settle').onclick=e=>{if(paid)return;paid=true;show('finance');status.textContent='Демонстрация: A принял 01 и 03; +$260, наличные $380; остался неисправный 02. Повтор заблокирован.';e.target.disabled=true};document.querySelector('#reset').onclick=()=>{tested=false;paid=false;step=0;document.querySelector('#settle').disabled=false;status.textContent='Демонстрация сброшена. Тест и выплата — независимые примеры, не сквозная симуляция экономики.';show('hud')};document.addEventListener('keydown',e=>{if(e.key==='Escape'){show('hud');nav.querySelector('button').focus()}});show('hud');</script></body></html>'''
(ROOT/'index.html').write_text(html,encoding='utf-8')
print(f'Generated {len(views)} UI screens, 4 production sheets and offline review tool.')
