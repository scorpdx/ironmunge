# download and verify rcedit (assume x64 host)
curl -Lo rcedit.exe https://github.com/electron/rcedit/releases/download/v1.1.1/rcedit-x64.exe
(echo -n "02e8e8c5d430d8b768980f517b62d7792d690982b9ba0f7e04163cbc1a6e7915 *rcedit.exe" | sha256sum -c -) || exit 1

# set ironmunge icon
rcedit pub-win-x64/ironmunge/ironmunge.exe --set-icon "assets/ironmunge.ico"
rcedit pub-win-x86/ironmunge/ironmunge.exe --set-icon "assets/ironmunge.ico"

# set SaveManager icon
rcedit pub-win-x64/SaveManager/SaveManager.exe --set-icon "assets/savemanager.ico"
rcedit pub-win-x86/SaveManager/SaveManager.exe --set-icon "assets/savemanager.ico"