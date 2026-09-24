import socket, struct, sys

host = 'aws-0-ap-northeast-2.pooler.supabase.com'
port = 5432

s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.settimeout(30)
print(f'Connecting to {host}:{port}...', flush=True)
s.connect((host, port))
print('Connected!', flush=True)

username = b'postgres.zutjkzifeyvqiygkznov'
payload = struct.pack('!II', 196608, 3 << 16)
payload += b'user' + b'\x00' + username + b'\x00' + b'\x00'
length = len(payload) + 4
packet = struct.pack('!I', length) + payload
s.sendall(packet)

try:
    data = s.recv(1024)
    print(f'Response ({len(data)} bytes): {data[:100]}', flush=True)
except socket.timeout:
    print('Timeout waiting for response', flush=True)
s.close()