# ModbusKUKA
A Modbus TCP Slave for KRC4 Controller

## WHY
KUKA not provide modbus TCP protocol, kukavarproxy only support 1 var request, it very slow. C3bridge support multi variables, But it not easy trans to different PLC's.

They all based on CROSS3 protocol, its a private protocol in kuka system. There have no documents. Luckly, they have dll file(PrimaryInterOp.Cross3Krc.dll) and we can use it.

## Performance
Cross3 lib communication with cross3 service use shared memory. Then cross3 trans it to "ADS" communication. But we don't have ADS API and no document here. So we just use Cross3 LIB.

A variable visit will cost about 2ms in cross3. You can monitor it in  Cross3LogViewer.

In this test we get 25 int32 variable result:

|NAME|TIME|NOTE|
|----|----|----|
|kukavarproxy|~90ms|
|c3bridge|~90ms|
|modbusKUKA|~100ms|
|kukavarproxy+TCP|~100ms|
|c3bridge+TCP|~100ms|single and multi was same|
|modbuskuka|~100ms|
|kukavarproxy+PLC|~400ms|
|c3bridge+PLC|~400ms|
|modbuskuka|~100ms|


