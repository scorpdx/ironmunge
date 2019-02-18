# download and verify MinGit 32
curl -Lo mingit32.zip https://github.com/git-for-windows/git/releases/download/v2.20.1.windows.1/MinGit-2.20.1-busybox-32-bit.zip
(echo -n "da0c03e3b6e77004efafd7c244dc62e92b4f78642d83234b0c62367c6ab2ad95 *mingit32.zip" | sha256sum -c -) || exit 1

# download and verify MinGit 64
curl -Lo mingit64.zip https://github.com/git-for-windows/git/releases/download/v2.20.1.windows.1/MinGit-2.20.1-busybox-64-bit.zip
(echo -n "9817ab455d9cbd0b09d8664b4afbe4bbf78d18b556b3541d09238501a749486c *mingit64.zip" | sha256sum -c -) || exit 1

# unzip MinGit 64 and add to pub-win-x64
(unzip mingit64.zip -d pub-win-x64/Resources/git) || exit 1

# unzip MinGit 32 and add to pub-win-x86
(unzip mingit32.zip -d pub-win-x86/Resources/git) || exit 1
