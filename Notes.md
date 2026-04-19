need to remove that : 
${USER_NAME} ALL=(root) NOPASSWD: /usr/bin/apt update
${USER_NAME} ALL=(root) NOPASSWD: /usr/bin/apt install -y wireguard wireguard-tools resolvconf

From isntall you can look other app how we made prepare wire guard client and do almost the same but do only prepar wireguard server and that would make a command that lets us execute in sudo only when we need. also check the initial setup so you can see where the file is copied iw ant  it the same but in our appc ontext
