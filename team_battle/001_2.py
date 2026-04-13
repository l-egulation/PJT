import sys
import socket
import heapq

HOST = '127.0.0.1'
PORT = 8747
ARGS = sys.argv[1] if len(sys.argv) > 1 else ''
sock = socket.socket()

map_data = [[]]  
allies = {}  
enemies = {}  
codes = []  

def init(nickname) -> str:
    try:
        sock.connect((HOST, PORT))
        init_command = f'INIT {nickname}' 
        return submit(init_command)
    except Exception as e:
        print('[ERROR] Failed to connect.')

def submit(string_to_send) -> str:
    try:
        send_data = ARGS + string_to_send + ' '
        sock.send(send_data.encode('utf-8'))
        return receive()
    except Exception as e:
        pass
    return None

def receive() -> str:
    try:
        game_data = (sock.recv(1024)).decode()
        if game_data and game_data[0].isdigit() and int(game_data[0]) > 0:
            return game_data
        close()
    except Exception as e:
        pass

def close():
    if sock is not None: sock.close()

def parse_data(game_data):
    game_data_rows = game_data.split('\n')
    row_index = 0

    header = game_data_rows[row_index].split(' ')
    map_height = int(header[0])  
    map_width = int(header[1])  
    num_of_allies = int(header[2])  
    num_of_enemies = int(header[3])  
    num_of_codes = int(header[4])  
    row_index += 1

    map_data.clear()
    map_data.extend([[ '' for c in range(map_width)] for r in range(map_height)])
    for i in range(0, map_height):
        col = game_data_rows[row_index + i].split(' ')
        for j in range(0, map_width):
            map_data[i][j] = col[j]
    row_index += map_height

    allies.clear()
    for i in range(row_index, row_index + num_of_allies):
        ally = game_data_rows[i].split(' ')
        ally_name = ally.pop(0)
        allies[ally_name] = ally
    row_index += num_of_allies

    enemies.clear()
    for i in range(row_index, row_index + num_of_enemies):
        enemy = game_data_rows[i].split(' ')
        enemy_name = enemy.pop(0)
        enemies[enemy_name] = enemy
    row_index += num_of_enemies

    codes.clear()
    for i in range(row_index, row_index + num_of_codes):
        codes.append(game_data_rows[i])

NICKNAME = '탱크_빵빵일-'
game_data = init(NICKNAME)

DIRECTIONS = [('U', -1, 0), ('D', 1, 0), ('L', 0, -1), ('R', 0, 1)]

def decrypt_caesar(ciphertext):
    """카이사르 암호 자동 해독"""
    ciphertext = ciphertext.strip()
    for shift in range(26):
        decrypted = ""
        for char in ciphertext:
            if 'A' <= char <= 'Z':
                decrypted += chr((ord(char) - ord('A') - shift) % 26 + ord('A'))
            elif 'a' <= char <= 'z':
                decrypted += chr((ord(char) - ord('a') - shift) % 26 + ord('a'))
            else:
                decrypted += char
        if "SSAFY" in decrypted.upper() or "BATTLE" in decrypted.upper():
            return decrypted
    return ciphertext

def get_supply_command(my_x, my_y):
    if not codes: return None
    for _, dx, dy in DIRECTIONS:
        nx, ny = my_x + dx, my_y + dy
        if 0 <= nx < len(map_data) and 0 <= ny < len(map_data[0]):
            if map_data[nx][ny] == 'F':
                return f'G {decrypt_caesar(codes[0])}'
    return None

def is_line_of_sight_clear(x1, y1, x2, y2):
    """포탄이 날아가는 경로 중간에 막히는 물건이 없는지 확인"""
    if x1 == x2:
        step = 1 if y2 > y1 else -1
        for y in range(y1 + step, y2, step):
            if map_data[x1][y] in ['T', 'R', 'F', 'H', 'M1', 'M2', 'M3'] or map_data[x1][y].startswith('E'): 
                return False
        return True
    elif y1 == y2:
        step = 1 if x2 > x1 else -1
        for x in range(x1 + step, x2, step):
            if map_data[x][y1] in ['T', 'R', 'F', 'H', 'M1', 'M2', 'M3'] or map_data[x][y1].startswith('E'): 
                return False
        return True
    return False

def get_shoot_command(my_x, my_y, normal_ammo):
    if normal_ammo <= 0: return None
    for d_name, dx, dy in DIRECTIONS:
        for dist in range(1, 4):
            nx, ny = my_x + dx * dist, my_y + dy * dist
            if not (0 <= nx < len(map_data) and 0 <= ny < len(map_data[0])): break
            target = map_data[nx][ny]
            if target == 'X' or target.startswith('E'): return f'{d_name} F'
            if target in ['T', 'R', 'F', 'H', 'M1', 'M2', 'M3']: break
    return None

def is_valid_target_pos(cx, cy, tx, ty, is_supply):
    """현재 위치가 타겟(또는 쏠 수 있는 위치)인지 판별"""
    if is_supply:
        return abs(cx - tx) + abs(cy - ty) <= 1
    else:
        if cx == tx and cy == ty: return True
        # 직접 밟지 못해도 거리가 3이하이고 시야가 뚫려있으면 도착한 것으로 간주
        if cx == tx and abs(cy - ty) <= 3 and is_line_of_sight_clear(cx, cy, tx, ty): return True
        if cy == ty and abs(cx - tx) <= 3 and is_line_of_sight_clear(cx, cy, tx, ty): return True
        return False

def get_next_optimal_move(my_x, my_y, target_x, target_y, ammo, is_supply):
    N, M = len(map_data), len(map_data[0])
    
    # [수정됨] 포탄 개수에 따른 상태 소멸 방지를 위해 dist를 Dictionary로 관리
    dist = {}
    dist[(my_x, my_y, ammo)] = 0
    pq = [(0, my_x, my_y, ammo, None, None)]
    
    while pq:
        cost, cx, cy, cur_ammo, first_dir, first_type = heapq.heappop(pq)
        
        # [수정됨] 바위로 막혀있더라도 시야만 나오면 탐색 성공!
        if is_valid_target_pos(cx, cy, target_x, target_y, is_supply):
            return f"{first_dir} {first_type}" if first_dir else "S"
            
        if cost > dist.get((cx, cy, cur_ammo), float('inf')):
            continue
            
        for d_name, dx, dy in DIRECTIONS:
            nx, ny = cx + dx, cy + dy
            if 0 <= nx < N and 0 <= ny < M:
                cell = map_data[nx][ny]
                next_cost = cost
                next_ammo = cur_ammo
                next_type = 'A'
                
                # 가중치 계산
                if cell in ['G', 'M']: 
                    next_cost += 1
                elif cell == 'S': 
                    next_cost += 10  # 모래 회피
                elif (cell == 'T' or cell.startswith('E')) and cur_ammo > 0:
                    next_cost += 50
                    next_ammo -= 1
                    next_type = 'F'
                else:
                    # 목적지가 X/F일 경우 그 자체는 밟지 않고 근처에서 리턴되므로 막힌 지형은 통과 불가
                    continue 

                state = (nx, ny, next_ammo)
                if next_cost < dist.get(state, float('inf')):
                    dist[state] = next_cost
                    n_dir = first_dir if first_dir else d_name
                    n_type = first_type if first_type else next_type
                    heapq.heappush(pq, (next_cost, nx, ny, next_ammo, n_dir, n_type))
                    
    return "S"  


while game_data is not None:
    print(f'----입력데이터----\n{game_data}\n----------------')
    parse_data(game_data)

    my_tank = allies.get('M')
    if not my_tank:
        game_data = submit('S')
        continue

    normal_round = int(my_tank[2])
    mega_round = int(my_tank[3])

    my_pos = [-1, -1]
    turret_pos = [-1, -1]
    supply_pos = [-1, -1]
    
    for i in range(len(map_data)):
        for j in range(len(map_data[0])):
            if map_data[i][j] == 'M':
                my_pos = [i, j]
            elif map_data[i][j] == 'X':
                turret_pos = [i, j]
            elif map_data[i][j] == 'F':
                supply_pos = [i, j]

    output = 'S'

    if my_pos[0] != -1:
        supply_cmd = get_supply_command(my_pos[0], my_pos[1])
        if supply_cmd:
            output = supply_cmd
        else:
            shoot_cmd = get_shoot_command(my_pos[0], my_pos[1], normal_round)
            if shoot_cmd:
                output = shoot_cmd
            else:
                is_supply_target = codes and supply_pos[0] != -1 and mega_round == 0
                target = supply_pos if is_supply_target else turret_pos
                
                output = get_next_optimal_move(my_pos[0], my_pos[1], target[0], target[1], normal_round, is_supply_target)

    game_data = submit(output)

close()