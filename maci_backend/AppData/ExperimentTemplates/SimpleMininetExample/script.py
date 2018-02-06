### ENV int mean_bw "The mean bandwidth at the bottleneck"
### ENV int delay "The delay per link"
### ENV int max_queue_size "The max queue size at the bottleneck link"

import framework
import os
import re

from mininet.net import Mininet
from mininet.link import TCLink
from mininet.log import setLogLevel
from mininet.node import CPULimitedHost
from mininet.topo import Topo

class StaticTopo(Topo):
  def build(self):
    h1 = self.addHost('h1')
    h2 = self.addHost('h2')

    s1 = self.addSwitch('s1')
    s2 = self.addSwitch('s2')

    # Topology: h1 -- s1 -- bottleneck -- s2 -- h2
    self.addLink(h1, s1, delay="{{delay}}ms")
    self.addLink(h2, s2, delay="{{delay}}ms")
    self.addLink(s1, s2, bw={{mean_bw}}, delay="{{delay}}ms", max_queue_size={{max_queue_size}})

def iperf(source, destination):
  destination.cmd('iperf -s -i 1 -y C > server.log &')
  source.cmd('iperf -c ' + str(destination.IP()) + ' -t 10 > client.log')
  framework.addLogfile("server.log")
  framework.addLogfile("client.log")

  server = open('server.log', 'r')
  bwsamples = []
  minTimestamp = None
  for line in server:
    # 20160622002425,10.0.0.2,5001,10.0.0.1,39345,4,0.0-1.0,14280,114240
    matchObj = re.match(r'(.*),(.*),(.*),(.*),(.*),(.*),(.*),(.*),(.*)', line, re.M)
    if matchObj:
        timestamp = float(matchObj.group(1))
        bwsample = float(matchObj.group(9)) / 1000.0 / 1000.0 # bits per second -> MBit
        bwsamples.append(bwsample)
        if minTimestamp is None:
            minTimestamp = timestamp
        framework.record("iperf_mbit_over_time", bwsample, timestamp - minTimestamp)
  framework.record("iperf_mbit_avg", sum(bwsamples) / len(bwsamples), offset=5)

if __name__ == '__main__':
  # Sometimes, old Minint instances crash.
  # We make sure that this does not crash following experiments on the same worker.
  os.system("mn -c")
  framework.start()

  topo = StaticTopo()
  net = Mininet(topo=topo, link=TCLink, host=CPULimitedHost)
  net.start()

  h1 = net.get('h1')
  h2 = net.get('h2')

  iperf(h1, h2)

  net.stop()
  framework.stop()
