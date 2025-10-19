This project is an app built with unity to enhance the streaming experience on popular streaming platforms (built for twitch primarily, with a modular design to accomodate other platforms in the future).
It is intended as a game/tool for a streamer to host and customize a persisted virtual cafe for viewers, represented by personalized avatars, using chat commands to interact.
It is meant to be docked idly at the bottom of the host's stream window alongside another game or fullscreen application.

## Getting started
This project requires to be cloned using a git client to work properly, unity 6 (tested with 6.2.6 and 6.0.47+) leveraging entities, burst, jobs and many other specific entities features and can be run without and with the chat interaction.  
for the chat interactions: This requires 2 twitch accounts, 1 for the streamer, another account for the bot (we recommend strongly to promote the bot as a [moderator](https://streamlabs.com/content-hub/post/how-to-make-someone-a-mod-on-twitch) )

## Usage
In the editor:
A. Without chat interaction:
1. Open the scene "OfflineDevSubscene"
2. Press play, an empty scene will appear, and a default character will spawn.
3. You can press the "B" key to open the build menu and start clicking on items to set up your environment (chairs are interactable), some objects can be rotated using "R" key
4. Click on the bag icon on the bottom right to open the player actions menu
5. click on the hand mirror, you can change your appearance using the UI that showed up

B. With chat interaction:
1. Find and select the credential asset at "Assets/GameProject/GameCustomAsset/TwitchChatUserMockAuthoring.asset"
2. Fill the "Channel" and "BotUserName" properties with the corresponding required accounts.
3. Open the scene "Main" and wait for everything to load
4. Make sure the last session of your default web browser is either unlogged on twitch or logged as the **BOT** account that you provided in step 2
5. Go back to unity and press play, the web browser will open and ask you to authentificate (if unlogged). when successful, a message will confirm you can close the page otherwise stop play and try again (twitch service or the webservice might be unavailable shortly)
6. To test as a viewer, open the twitch channel https://www.twitch.tv/ and add the channel name you used in the credential asset at the end (use the bot account for testing but any viewer can do this)
7. In the twitch chat, type !join (by default) the avatar will appear in the game tab on unity
8. You can type !roll in the chat to change the avatar appearance randomly.

In a build:
the project can be build (tested only on windows) from this project and conveniently has a build with chat interaction ready,
it does share the same requirement (2 twitch accounts, channel and a bot created)
Here the steps to make this work in a build.
1. Find the json file "TwitchChatBotSettings.json" in the "Releases/UserSettings/" folder and open it with your favourite text editor then save
2. see step B.2.
3. see step B.4.
4. Start the app "UnityStreamCafe.exe" in the release folder, if the bot user name and channel are correct a character should appear in the middle of the screen
5. to test follow steps B.6,7,8


## Notable
Character's appearances, game currencies, built objects are all persisted locally as files on the same folder. 
if you start without chat interaction, you might see those objects appear again in another scene as a result, which is to be expected.
The persistence in the build is separated though.

## Status
The UI Layout is placeholder and will be changed in the future.
The project is still being developed internally, some bugs might appear.

## Mentions
[TwitchLib](https://github.com/TwitchLib/TwitchLib)
