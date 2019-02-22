# download and verify MinGit 64
curl -Lo mingit64.zip https://github.com/git-for-windows/git/releases/download/v2.20.1.windows.1/MinGit-2.20.1-busybox-64-bit.zip
(echo -n "9817ab455d9cbd0b09d8664b4afbe4bbf78d18b556b3541d09238501a749486c *mingit64.zip" | sha256sum -c -) || exit 1

# unzip MinGit 64 and add to assets
(unzip -o mingit64.zip -d $(Build.SourcesDirectory)/assets/git) || exit 1