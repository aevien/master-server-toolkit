<color=orange>***MasterServerAndClient***
THIS EXAMPLE DEMONSTRATES MASTER SERVER STARTER AND CONNECTION TO MASTER</color>

1. Click Play button
2. If <color=yellow>"AutoStartInEditor"</color> checkbox on <color=yellow>"--MASTER_SERVER"</color> object is checked on then master server will be started automatically on Play with all its modules.
3. Press <color=yellow>"~"</color> key to open command terminal and input <color=yellow>"help"</color> command to get list of the available terminal commands. We need <color=yellow>"client.connect"</color> and <color=yellow>"client.disconnect"</color> commands
4. If <color=yellow>"ConnectOnStart"</color> checkbox on <color=yellow>"--CONNECTION_TO_MASTER"</color> object is checked on then client will try to automatically connect to master server. Otherwise <color=yellow>"client.connect"</color> command will help us to connect to master manualy. Try to input <color=yellow>"client.connect"</color> command, then IP address, then Port and see the result. Example: <color=yellow>client.connect 192.0.1.62 5000</color>
5. Input <color=yellow>"client.ping"</color> command and you will get message from master. Cool!

PS: If you wish to test builds click <color=yellow>"Tools/MSF/Build/Demos/Basic Connection"</color> and build your standalone version.