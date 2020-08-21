<color=orange>***MasterServerAndClient***
THIS EXAMPLE DEMONSTRATES CONNECTION TO MASTER</color>

1. Click Play button
2. Press <color=yellow>"~"</color> key to open command terminal and input <color=yellow>"help"</color> command to get list of the available terminal commands. We need <color=yellow>"client.connect"</color> and <color=yellow>"client.disconnect"</color> commands
3. If <color=yellow>"ConnectOnStart"</color> checkbox on <color=yellow>"--CONNECTION_TO_MASTER"</color> object is checked on then client will try to automatically connect to master server. Otherwise <color=yellow>"client.connect"</color> command will help us to connect to master manualy. Try to input <color=yellow>"client.connect"</color> command, then IP address, then Port and see the result. Example: <color=yellow>client.connect 192.0.1.62 5000</color>
4. Input <color=yellow>"client.ping"</color> command and you will get message from master. Cool!

PS: If you wish to test builds click <color=yellow>"Tools/MSF/Build/Demos/Basic Connection"</color> and build your standalone version.