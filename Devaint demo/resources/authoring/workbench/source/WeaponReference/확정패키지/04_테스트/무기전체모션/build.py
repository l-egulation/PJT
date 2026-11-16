from pathlib import Path
import json
HERE=Path(__file__).resolve().parent
frozen=json.loads((HERE/'확정미리보기데이터.json').read_text(encoding='utf8'))
html=(HERE/'page.html').read_text(encoding='utf8').replace('/*ASSETS*/',json.dumps(frozen,ensure_ascii=False,separators=(',',':')))
(HERE/'전체모션.html').write_text(html,encoding='utf8')
print('Built confirmed four-weapon gallery from frozen snapshot.')
