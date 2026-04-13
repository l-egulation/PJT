import sys
import socket
import heapq
import itertools

##############################
# 메인 프로그램 통신 변수 정의
##############################
HOST = '127.0.0.1'
PORT = 8747
ARGS = sys.argv[1] if len(sys.argv) > 1 else ''
sock = socket.socket()

def init(nickname):
    try:
        print(f'[STATUS] Trying to connect to {HOST}:{PORT}...')
        sock.connect((HOST, PORT))
        print('[STATUS] Connected')
        init_command = f'INIT {nickname}'
        return submit(init_command)
    except Exception as e:
        print('[ERROR] Failed to connect.')
        print(e)

def submit(string_to_send):
    try:
        # ARGS 뒤에 명령어를 붙여서 전송 (띄어쓰기 오류 방지)
        send_data = ARGS + string_to_send + ' '
        sock.send(send_data.encode('utf-8'))
        return receive()
    except Exception as e:
        print('[ERROR] Failed to send data.')
    return None

def receive():
    try:
        game_data = (sock.recv(4096)).decode()
        if game_data and game_data[0].isdigit() and int(game_data[0]) > 0:
            return game_data
        close()
    except Exception as e:
        print('[ERROR] Failed to receive data.')

def close():
    try:
        if sock is not None:
            sock.close()
        print('[STATUS] Connection closed')
    except Exception as e:
        pass

##############################
# 입력 데이터 변수 정의
##############################
map_data = [[]]  
my_allies = {}  
enemies = {}  
codes = []  

def parse_data(game_data):
    game_data_rows = game_data.split('\n')
    row_index = 0

    header = game_data_rows[row_index].split(' ')
    map_height = int(header[0]) if len(header) >= 1 else 0 
    map_width = int(header[1]) if len(header) >= 2 else 0  
    num_of_allies = int(header[2]) if len(header) >= 3 else 0  
    num_of_enemies = int(header[3]) if len(header) >= 4 else 0  
    num_of_codes = int(header[4]) if len(header) >= 5 else 0  
    row_index += 1

    map_data.clear()
    map_data.extend([[ '' for _ in range(map_width)] for _ in range(map_height)])
    for i in range(0, map_height):
        if row_index + i < len(game_data_rows):
            col = game_data_rows[row_index + i].split(' ')
            for j in range(0, len(col)):
                if j < map_width:
                    map_data[i][j] = col[j]
    row_index += map_height

    my_allies.clear()
    for i in range(row_index, row_index + num_of_allies):
        if i < len(game_data_rows):
            ally = game_data_rows[i].split(' ')
            ally_name = ally.pop(0) if len(ally) >= 1 else '-'
            my_allies[ally_name] = ally
    row_index += num_of_allies

    enemies.clear()
    for i in range(row_index, row_index + num_of_enemies):
        if i < len(game_data_rows):
            enemy = game_data_rows[i].split(' ')
            enemy_name = enemy.pop(0) if len(enemy) >= 1 else '-'
            enemies[enemy_name] = enemy
    row_index += num_of_enemies

    codes.clear()
    for i in range(row_index, row_index + num_of_codes):
        if i < len(game_data_rows) and len(game_data_rows[i].strip()) > 0:
            codes.append(game_data_rows[i].strip())

###################################
# ★ [핵심] 플레이어 역할 설정 ★
# 팀원 3명이 각각 1, 2, 3으로 다르게 설정해서 제출하세요!
###################################
PLAYER_ROLE = 2  # 1: 돌격대장, 2: 암호해독/폭탄마, 3: 적 탱크 암살자
NICKNAME = f'대전2_이규재_{PLAYER_ROLE}' # 본인 지역/이름으로 수정
game_data = init(NICKNAME)

###################################
# 알고리즘 함수 구현부
###################################
DIRS = [(0,1), (1,0), (0,-1), (-1,0)] # 우, 하, 좌, 상

# [수정완료] 서버가 요구하는 띄어쓰기 규정("R A", "R F") 완벽 적용
MOVE_CMDS = ["R A", "D A", "L A", "U A"]
FIRE_CMDS = ["R F", "D F", "L F", "U F"]

mega_obtained = False

# 카이사르 암호 자동 해독 (S->B 기준 Shift 17)
def decode_cipher(cipher_text):
    if not cipher_text: return ""
    shift = (ord(cipher_text[0]) - ord('B')) % 26
    decoded = ""
    for ch in cipher_text:
        if ch.isalpha():
            decoded += chr((ord(ch) - ord('A') - shift + 26) % 26 + ord('A'))
        else:
            decoded += ch
    return decoded

# 특정 목표물 위치 찾기
def find_target(grid, target_mark):
    for r in range(len(grid)):
        for c in range(len(grid[0])):
            if grid[r][c] == target_mark:
                return (r, c)
    return None

# 가장 가까운 적 탱크 찾기 (Player 3 용도)
def get_closest_enemy(r, c, grid):
    enemies_pos = []
    for i in range(len(grid)):
        for j in range(len(grid[0])):
            if grid[i][j].startswith('E') and grid[i][j] != 'E': # E1, E2, E3
                enemies_pos.append((i, j))
    if not enemies_pos: return None
    enemies_pos.sort(key=lambda p: abs(r - p[0]) + abs(c - p[1]))
    return enemies_pos[0]

# 사거리 3칸 저격 체크 (아군 오폭 방지 포함)
def get_attack_cmd(r, c, target_r, target_c, grid, has_mega):
    dist = abs(r - target_r) + abs(c - target_c)
    if (r == target_r or c == target_c) and 1 <= dist <= 3:
        d_idx = -1
        if c < target_c: d_idx = 0 # 우
        elif r < target_r: d_idx = 1 # 하
        elif c > target_c: d_idx = 2 # 좌
        elif r > target_r: d_idx = 3 # 상

        # 가는 길에 바위(R)나 아군(M, A, H)이 있으면 쏘지 않음
        for k in range(1, dist):
            check_r, check_c = r + DIRS[d_idx][0]*k, c + DIRS[d_idx][1]*k
            cell = grid[check_r][check_c]
            if cell == 'R' or cell == 'H' or cell.startswith('M') or cell == 'A':
                return None
        
        # [수정완료] 메가 포탄 발사 시에도 "R F M" 처럼 띄어쓰기 유지
        cmd = FIRE_CMDS[d_idx] + (" M" if has_mega else "")
        return cmd
    return None

# 다익스트라(Dijkstra) 기반 최적 경로 탐색 (모래 페널티 고려)
def get_best_actions(grid, start, target, target_type, has_mega):
    counter = itertools.count()
    pq = [(0, next(counter), start[0], start[1], [])]
    visited = [[float('inf')] * len(grid[0]) for _ in range(len(grid))]
    visited[start[0]][start[1]] = 0

    best_path = []
    min_cost = float('inf')

    while pq:
        cost, _, r, c, actions = heapq.heappop(pq)
        if cost >= min_cost: continue

        # Player 2가 보급소(F)를 타겟으로 잡았을 때, 인접(거리 1)하면 탐색 종료
        if target_type == 'F':
            if abs(r - target[0]) + abs(c - target[1]) == 1:
                return actions
        else:
            # 포탑(X) 또는 적(E) 타겟일 경우 저격 가능한지 체크
            atk_cmd = get_attack_cmd(r, c, target[0], target[1], grid, has_mega)
            if atk_cmd:
                if cost + 1 < min_cost:
                    min_cost = cost + 1
                    best_path = actions + [atk_cmd]
                continue

        # 4방향 이동 탐색
        for i in range(4):
            nr, nc = r + DIRS[i][0], c + DIRS[i][1]
            if 0 <= nr < len(grid) and 0 <= nc < len(grid[0]):
                cell = grid[nr][nc]

                # 절대 이동 불가 지형: 물(W), 바위(R), 보급소(F), 아군(M1, M2, H)
                if cell in ['W', 'R', 'F', 'H'] or (cell.startswith('M') and cell != 'M') or cell == 'A':
                    continue

                new_cost = cost
                new_actions = list(actions)

                # 땅(G) 또는 내 시작점(M)
                if cell == 'G' or cell == 'M':
                    new_cost += 1
                    new_actions.append(MOVE_CMDS[i])
                # 모래(S): 체력 -10 페널티가 있으므로 가급적 우회하도록 코스트 대폭 증가
                elif cell == 'S':
                    new_cost += 5
                    new_actions.append(MOVE_CMDS[i])
                # 나무(T) 또는 움직이는 적 탱크(E~): 부수고 지나가기 (2턴 소모)
                elif cell == 'T' or cell.startswith('E'):
                    new_cost += 2
                    new_actions.append(FIRE_CMDS[i] + (" M" if has_mega else ""))
                    new_actions.append(MOVE_CMDS[i])
                elif cell == 'X':
                    continue # X는 밟지 않고 위에서 저격 처리됨

                if new_cost < visited[nr][nc]:
                    visited[nr][nc] = new_cost
                    heapq.heappush(pq, (new_cost, next(counter), nr, nc, new_actions))

    return best_path

###################################
# 메인 루프
###################################
while game_data is not None:
    parse_data(game_data)
    
    # [수정완료] 내 위치 찾기 안전장치 (M을 찾고 없으면 A를 찾음)
    my_pos = find_target(map_data, 'M')
    if not my_pos:
        my_pos = find_target(map_data, 'A')
    
    if not my_pos:
        game_data = submit('S') # 죽었거나 예외 상황일 때 대기
        continue

    # 메가 포탄 보유 여부 확인
    has_mega = False
    if 'M' in my_allies and len(my_allies['M']) >= 4:
        has_mega = int(my_allies['M'][3]) > 0
    elif 'A' in my_allies and len(my_allies['A']) >= 4:
        has_mega = int(my_allies['A'][3]) > 0

    output = 'S'

    # [최우선 행동] 보급소 옆에서 암호문을 받았고, 아직 메가를 안 얻었다면 해독
    if len(codes) > 0 and not mega_obtained:
        output = "G " + decode_cipher(codes[0])
        mega_obtained = True
    else:
        # [역할별 타겟팅 로직]
        target_pos = None
        target_type = 'X'

        if PLAYER_ROLE == 1:
            target_pos = find_target(map_data, 'X')
        
        elif PLAYER_ROLE == 2:
            if not mega_obtained and find_target(map_data, 'F'):
                target_pos = find_target(map_data, 'F')
                target_type = 'F'
            else:
                target_pos = find_target(map_data, 'X')
        
        elif PLAYER_ROLE == 3:
            target_pos = get_closest_enemy(my_pos[0], my_pos[1], map_data)
            target_type = 'E'
            if not target_pos:
                target_pos = find_target(map_data, 'X')
                target_type = 'X'

        # 매 턴마다 다익스트라(Dijkstra) 경로를 새로 계산
        if target_pos:
            actions = get_best_actions(map_data, my_pos, target_pos, target_type, has_mega)
            if actions:
                output = actions[0] # 계산된 최적 경로의 첫 번째 행동만 즉시 수행

    # 서버로 커맨드 전송
    game_data = submit(output)

close()