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
# 1: 돌격대장 (모래/나무 무시하고 적 본진 직진)
# 2: 수비수 (아군 포탑 방어, 베이스 근처 적 요격)
# 3: 암살자 (내 위치에서 가장 가까운 적 요격)
###################################
PLAYER_ROLE = 1  
NICKNAME = f'대전2_이규재_{PLAYER_ROLE}' 
game_data = init(NICKNAME)

###################################
# 알고리즘 함수 구현부
###################################
DIRS = [(0,1), (1,0), (0,-1), (-1,0)] 
MOVE_CMDS = ["R A", "D A", "L A", "U A"]
FIRE_CMDS = ["R F", "D F", "L F", "U F"]

mega_obtained = False

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

def find_target(grid, target_mark):
    for r in range(len(grid)):
        for c in range(len(grid[0])):
            if grid[r][c] == target_mark:
                return (r, c)
    return None

# [Player 3 용도] 내 위치에서 가장 가까운 적 찾기
def get_closest_enemy(r, c, grid):
    enemies_pos = []
    for i in range(len(grid)):
        for j in range(len(grid[0])):
            if grid[i][j].startswith('E') and grid[i][j] != 'E': 
                enemies_pos.append((i, j))
    if not enemies_pos: return None
    enemies_pos.sort(key=lambda p: abs(r - p[0]) + abs(c - p[1]))
    return enemies_pos[0]

# [Player 2 용도] 아군 베이스(H)에서 가장 가까운 적 찾기
def get_closest_enemy_to_base(base_r, base_c, grid):
    enemies_pos = []
    for i in range(len(grid)):
        for j in range(len(grid[0])):
            if grid[i][j].startswith('E') and grid[i][j] != 'E': 
                enemies_pos.append((i, j))
    if not enemies_pos: return None
    enemies_pos.sort(key=lambda p: abs(base_r - p[0]) + abs(base_c - p[1]))
    return enemies_pos[0]

# 적 체력 60 이상인지 판별하는 유틸 함수
def should_use_mega(target_name, has_mega):
    if not has_mega: return False
    if target_name in enemies:
        enemy_hp = int(enemies[target_name][0])
        return enemy_hp >= 60
    return False

# 기회주의적 견제 사격
def check_opportunistic_shoot(r, c, grid, has_mega):
    for d_idx, (dr, dc) in enumerate(DIRS):
        for k in range(1, 4): 
            nr, nc = r + dr*k, c + dc*k
            if 0 <= nr < len(grid) and 0 <= nc < len(grid[0]):
                cell = grid[nr][nc]
                if cell in ['T', 'R', 'H'] or (cell.startswith('M') and cell != 'M') or cell == 'A':
                    break
                    
                if cell == 'X' or cell.startswith('E'):
                    use_mega = should_use_mega(cell, has_mega)
                    return FIRE_CMDS[d_idx] + (" M" if use_mega else "")
    return None

# 사거리 3칸 저격 체크
def get_attack_cmd(r, c, target_r, target_c, grid, has_mega):
    dist = abs(r - target_r) + abs(c - target_c)
    if (r == target_r or c == target_c) and 1 <= dist <= 3:
        d_idx = -1
        if c < target_c: d_idx = 0 
        elif r < target_r: d_idx = 1 
        elif c > target_c: d_idx = 2 
        elif r > target_r: d_idx = 3 

        for k in range(1, dist):
            check_r, check_c = r + DIRS[d_idx][0]*k, c + DIRS[d_idx][1]*k
            cell = grid[check_r][check_c]
            if cell == 'R' or cell == 'H' or cell.startswith('M') or cell == 'A':
                return None
        
        target_cell = grid[target_r][target_c]
        use_mega = should_use_mega(target_cell, has_mega)
        return FIRE_CMDS[d_idx] + (" M" if use_mega else "")
    return None

# 다익스트라(Dijkstra) 최적 경로 탐색
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

        atk_cmd = get_attack_cmd(r, c, target[0], target[1], grid, has_mega)
        if atk_cmd:
            if cost + 1 < min_cost:
                min_cost = cost + 1
                best_path = actions + [atk_cmd]
            continue

        for i in range(4):
            nr, nc = r + DIRS[i][0], c + DIRS[i][1]
            if 0 <= nr < len(grid) and 0 <= nc < len(grid[0]):
                cell = grid[nr][nc]

                if cell in ['W', 'R', 'F', 'H'] or (cell.startswith('M') and cell != 'M') or cell == 'A':
                    continue

                new_cost = cost
                new_actions = list(actions)

                if cell == 'G' or cell == 'M':
                    new_cost += 1
                    new_actions.append(MOVE_CMDS[i])
                    
                elif cell == 'S':
                    # ★ 돌격대장(1번)은 체력 감소 무시하고 모래를 비용 1로 간주
                    new_cost += (1 if PLAYER_ROLE == 1 else 5)
                    new_actions.append(MOVE_CMDS[i])
                    
                elif cell == 'T':
                    # ★ 돌격대장은 나무 우회(2턴)보다 파괴 직진(1.5턴 취급)을 선호하도록 세팅
                    new_cost += (1.5 if PLAYER_ROLE == 1 else 2)
                    new_actions.append(FIRE_CMDS[i])
                    new_actions.append(MOVE_CMDS[i])
                    
                elif cell.startswith('E'):
                    new_cost += 2
                    use_mega = should_use_mega(cell, has_mega)
                    new_actions.append(FIRE_CMDS[i] + (" M" if use_mega else ""))
                    new_actions.append(MOVE_CMDS[i])
                    
                elif cell == 'X':
                    continue 

                if new_cost < visited[nr][nc]:
                    visited[nr][nc] = new_cost
                    heapq.heappush(pq, (new_cost, next(counter), nr, nc, new_actions))

    return best_path

###################################
# 메인 루프
###################################
while game_data is not None:
    parse_data(game_data)
    
    my_pos = find_target(map_data, 'M')
    if not my_pos:
        my_pos = find_target(map_data, 'A')
    
    if not my_pos:
        game_data = submit('S') 
        continue

    has_mega = False
    if 'M' in my_allies and len(my_allies['M']) >= 4:
        has_mega = int(my_allies['M'][3]) > 0
    elif 'A' in my_allies and len(my_allies['A']) >= 4:
        has_mega = int(my_allies['A'][3]) > 0

    output = 'S'

    # [전원 공통 행동] 보급소 옆에서 암호문을 받았고, 아직 메가를 안 얻었다면 누구나 즉시 해독!
    if len(codes) > 0 and not mega_obtained:
        output = "G " + decode_cipher(codes[0])
        mega_obtained = True
    else:
        # 시야 내 적 무한 견제
        opp_shoot = check_opportunistic_shoot(my_pos[0], my_pos[1], map_data, has_mega)
        
        if opp_shoot:
            output = opp_shoot
        else:
            target_pos = None
            target_type = 'X'

            if PLAYER_ROLE == 1:
                # [돌격대장] 무조건 적 본진 직진
                target_pos = find_target(map_data, 'X')
            
            elif PLAYER_ROLE == 2:
                # [수비수] 아군 포탑(H) 기준 가장 가까운 적 추적
                base_pos = find_target(map_data, 'H')
                if base_pos:
                    target_pos = get_closest_enemy_to_base(base_pos[0], base_pos[1], map_data)
                    target_type = 'E'
                
                # 아군 포탑이 없거나, 적이 전멸했다면 본진(X) 공격 합류
                if not target_pos:
                    target_pos = find_target(map_data, 'X')
                    target_type = 'X'
            
            elif PLAYER_ROLE == 3:
                # [암살자] 내 위치 기준 가장 가까운 적 추적
                target_pos = get_closest_enemy(my_pos[0], my_pos[1], map_data)
                target_type = 'E'
                
                if not target_pos:
                    target_pos = find_target(map_data, 'X')
                    target_type = 'X'

            if target_pos:
                actions = get_best_actions(map_data, my_pos, target_pos, target_type, has_mega)
                if actions:
                    output = actions[0]

    game_data = submit(output)

close()