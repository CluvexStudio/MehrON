import asyncio
import os
import socket
import sys
import traceback
import threading
import json

if sys.platform == "win32":
    try:
        if sys.stdout and hasattr(sys.stdout, "reconfigure"):
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        if sys.stderr and hasattr(sys.stderr, "reconfigure"):
            sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# from utils.proxy_protocols import parse_vless_protocol
from utils.network_tools import get_default_interface_ipv4
from utils.packet_templates import ClientHelloMaker
from fake_tcp import FakeInjectiveConnection, FakeTcpInjector


def get_exe_dir():
    """Returns the directory where the .exe (or script) is located."""
    if getattr(sys, 'frozen', False):
        # Running as a PyInstaller EXE
        return os.path.dirname(sys.executable)
    else:
        # Running as a normal Python script
        return os.path.dirname(os.path.abspath(__file__))


def setup_windivert_driver():
    if sys.platform != "win32":
        return
    try:
        import shutil
        import subprocess

        exe_dir = get_exe_dir()
        candidates = [
            os.path.join(exe_dir, "WinDivert64.sys"),
            os.path.join(exe_dir, "_internal", "pydivert", "windivert_dll", "WinDivert64.sys"),
            os.path.join(exe_dir, "WinDivert.sys"),
            os.path.join(exe_dir, "_internal", "pydivert", "windivert_dll", "WinDivert.sys"),
        ]
        source_sys = None
        for cand in candidates:
            if os.path.exists(cand):
                source_sys = cand
                break

        system_root = os.environ.get("SystemRoot", r"C:\Windows")
        system_drivers_dir = os.path.join(system_root, "System32", "drivers")
        driver_dest = os.path.join(system_drivers_dir, "WinDivert64.sys")

        driver_path = source_sys
        if source_sys and os.path.exists(system_drivers_dir):
            try:
                shutil.copy2(source_sys, driver_dest)
                driver_path = driver_dest
            except Exception:
                pass

        if driver_path:
            quoted = f'"{driver_path}"'
            subprocess.run(["sc.exe", "create", "WinDivert", f"binPath= {quoted}", "type= kernel"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            subprocess.run(["sc.exe", "config", "WinDivert", f"binPath= {quoted}", "type= kernel"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            subprocess.run(["sc.exe", "start", "WinDivert"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except Exception as ex:
        print(f"Driver setup warning: {ex}")


setup_windivert_driver()


# Build the path to config.json
config_path = os.path.join(get_exe_dir(), 'config.json')

# Load the config
with open(config_path, 'r') as f:
    config = json.load(f)

LISTEN_HOST = config["LISTEN_HOST"]
LISTEN_PORT = config["LISTEN_PORT"]
FAKE_SNI = config["FAKE_SNI"].encode()
CONNECT_IP = config["CONNECT_IP"]
CONNECT_PORT = config["CONNECT_PORT"]
INTERFACE_IPV4 = get_default_interface_ipv4(CONNECT_IP)
DATA_MODE = "tls"
BYPASS_METHOD = "wrong_seq"

##################

fake_injective_connections: dict[tuple, FakeInjectiveConnection] = {}


async def relay_main_loop(sock_1: socket.socket, sock_2: socket.socket, peer_task: asyncio.Task,
                          first_prefix_data: bytes):
    try:
        loop = asyncio.get_running_loop()
        while True:
            try:
                data = await loop.sock_recv(sock_1, 65575)
                if not data:
                    break
                if first_prefix_data:
                    data = first_prefix_data + data
                    first_prefix_data = b""
                sent_len = await loop.sock_sendall(sock_2, data)
                if sent_len != len(data):
                    break
            except Exception:
                break
    except Exception:
        pass
    finally:
        try:
            sock_1.close()
        except Exception:
            pass
        try:
            sock_2.close()
        except Exception:
            pass
        if not peer_task.done():
            peer_task.cancel()


async def handle(incoming_sock: socket.socket, incoming_remote_addr):
    outgoing_sock = None
    fake_injective_conn = None
    try:
        loop = asyncio.get_running_loop()
        if DATA_MODE == "tls":
            fake_data = ClientHelloMaker.get_client_hello_with(os.urandom(32), os.urandom(32), FAKE_SNI,
                                                               os.urandom(32))
        else:
            return

        is_ipv6 = ":" in CONNECT_IP
        outgoing_sock = socket.socket(socket.AF_INET6 if is_ipv6 else socket.AF_INET, socket.SOCK_STREAM)
        outgoing_sock.setblocking(False)
        try:
            if INTERFACE_IPV4 and not is_ipv6:
                outgoing_sock.bind((INTERFACE_IPV4, 0))
            else:
                outgoing_sock.bind(('::' if is_ipv6 else '0.0.0.0', 0))
        except Exception:
            pass
        outgoing_sock.setsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE, 1)
        outgoing_sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_KEEPIDLE, 11)
        outgoing_sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_KEEPINTVL, 2)
        outgoing_sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_KEEPCNT, 3)
        src_port = outgoing_sock.getsockname()[1]

        fake_injective_conn = FakeInjectiveConnection(outgoing_sock, INTERFACE_IPV4, CONNECT_IP, src_port, CONNECT_PORT,
                                                      fake_data,
                                                      BYPASS_METHOD, incoming_sock)
        fake_injective_connections[fake_injective_conn.id] = fake_injective_conn

        try:
            await asyncio.wait_for(loop.sock_connect(outgoing_sock, (CONNECT_IP, CONNECT_PORT)), timeout=5.0)
        except Exception:
            return

        if BYPASS_METHOD == "wrong_seq":
            try:
                await asyncio.wait_for(fake_injective_conn.t2a_event.wait(), 2.0)
            except Exception:
                pass

        if fake_injective_conn and fake_injective_conn.id in fake_injective_connections:
            fake_injective_conn.monitor = False
            del fake_injective_connections[fake_injective_conn.id]

        oti_task = asyncio.create_task(
            relay_main_loop(outgoing_sock, incoming_sock, asyncio.current_task(), b""))
        await relay_main_loop(incoming_sock, outgoing_sock, oti_task, b"")
    except Exception:
        pass
    finally:
        if fake_injective_conn and fake_injective_conn.id in fake_injective_connections:
            fake_injective_conn.monitor = False
            try:
                del fake_injective_connections[fake_injective_conn.id]
            except KeyError:
                pass
        if outgoing_sock:
            try:
                outgoing_sock.close()
            except Exception:
                pass
        try:
            incoming_sock.close()
        except Exception:
            pass


async def main():
    mother_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    mother_sock.setblocking(False)
    mother_sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    mother_sock.bind((LISTEN_HOST, LISTEN_PORT))
    mother_sock.setsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE, 1)
    mother_sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_KEEPIDLE, 11)
    mother_sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_KEEPINTVL, 2)
    mother_sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_KEEPCNT, 3)
    mother_sock.listen()
    loop = asyncio.get_running_loop()
    while True:
        try:
            incoming_sock, addr = await loop.sock_accept(mother_sock)
            incoming_sock.setblocking(False)
            incoming_sock.setsockopt(socket.SOL_SOCKET, socket.SO_KEEPALIVE, 1)
            incoming_sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_KEEPIDLE, 11)
            incoming_sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_KEEPINTVL, 2)
            incoming_sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_KEEPCNT, 3)
            asyncio.create_task(handle(incoming_sock, addr))
        except Exception:
            await asyncio.sleep(0.05)


if __name__ == "__main__":
    setup_windivert_driver()

    if ":" in CONNECT_IP:
        w_filter = f"tcp and (ipv6.DstAddr == {CONNECT_IP} or ipv6.SrcAddr == {CONNECT_IP})"
    else:
        w_filter = f"tcp and (ip.DstAddr == {CONNECT_IP} or ip.SrcAddr == {CONNECT_IP})"

    fake_tcp_injector = FakeTcpInjector(w_filter, fake_injective_connections)
    threading.Thread(target=fake_tcp_injector.run, args=(), daemon=True).start()
    print("@patterniha")
    asyncio.run(main())
