import sys
import time
from abc import ABC, abstractmethod

from pydivert import WinDivert, Packet


# from pydivert.consts import *


class TcpInjector(ABC):
    def __init__(self, w_filter: str):
        self.w_filter = w_filter
        try:
            self.w: WinDivert = WinDivert(w_filter)
        except Exception:
            self.w = None

    def safe_send(self, packet: Packet, recalculate_checksum: bool = False):
        try:
            if hasattr(self, "w") and self.w and self.w.is_open:
                self.w.send(packet, recalculate_checksum)
        except Exception:
            pass

    @abstractmethod
    def inject(self, packet: Packet):
        raise NotImplementedError("Not implemented")

    def run(self):
        while True:
            try:
                if not hasattr(self, "w") or self.w is None:
                    self.w = WinDivert(self.w_filter)
                with self.w:
                    while True:
                        packet = self.w.recv(65575)
                        self.inject(packet)
            except Exception as ex:
                print(f"TcpInjector error: {ex}", file=sys.stderr)
                try:
                    if hasattr(self, "w") and self.w:
                        self.w.close()
                except Exception:
                    pass
                time.sleep(1)
