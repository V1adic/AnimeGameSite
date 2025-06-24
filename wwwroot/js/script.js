if (typeof bigInt === 'undefined') {
    console.error('Библиотека bigInt не загружена.');
    throw new Error('Библиотека bigInt не загружена.');
}

const N = bigInt("167609434410335061345139523764350090260135525329813904557420930309800865859473551531551523800013916573891864789934747039010546328480848979516637673776605610374669426214776197828492691384519453218253702788022233205683635831626913357154941914129985489522629902540768368409482248290641036967659389658897350067939");
const g = bigInt(2);

function generateRandomBigInt(bits) {
    const byteLength = Math.ceil(bits / 8);
    const randomBytes = new Uint8Array(byteLength);
    crypto.getRandomValues(randomBytes);
    return bigInt.fromArray([...randomBytes], 256);
}

function combineBytes(...args) {
    return args.join("");
}

function computeSha256(bytes) {
    const hashHex = sha256(bytes);
    return bigInt(hashHex, 16);
}

function base64ToUint8Array(base64) {
    const binaryString = atob(base64); // декодирует base64 в бинарную строку
    const len = binaryString.length;
    const bytes = new Uint8Array(len);

    for (let i = 0; i < len; i++) {
        bytes[i] = binaryString.charCodeAt(i); // получаем код каждого символа
    }

    return bytes;
}

class SrpClient {
    constructor(password) {
        this.password = password;
        this.a = generateRandomBigInt(256);
    }

    generatePublicKey() {
        this.A = g.modPow(this.a, N);
        return this.A;
    }

    async computeSessionKey(B, salt) {
        this.salt = bigInt(salt);
        const x = computeSha256(this.salt.toString() + this.password);
        const u = computeSha256(combineBytes(this.A.toString(), B.toString()));
        const k = computeSha256(combineBytes(N.toString(), g.toString()));
        const gX = g.modPow(x, N);
        const kTimesGX = k.times(gX).mod(N);
        const base = (B.plus(N).minus(kTimesGX)).mod(N);
        const exponent = this.a.plus(u.times(x));
        this.S = base.modPow(exponent, N);
        if (this.S.lesser(0)) this.S = this.S.plus(N);
        this.K = computeSha256(this.S.toString());
        const hashBuffer = await sha256.arrayBuffer(this.S.toString());
        return new Uint8Array(hashBuffer);
    }

    computeClientProof(B) {
        return computeSha256(combineBytes(this.A.toString(), B.toString(), this.K.toString()));
    }

    verifyServerProof(M1, M2) {
        const computedM2 = computeSha256(combineBytes(this.A.toString(), M1.toString(), this.K.toString()));
        return computedM2.eq(M2);
    }
}

async function register() {
    const username = document.getElementById("registerUsername").value;
    const password = document.getElementById("registerPassword").value;
    const client = new SrpClient(password);
    client.salt = generateRandomBigInt(128);
    const x = computeSha256(client.salt.toString() + password);
    const v = g.modPow(x, N);

    try {
        const response = await fetch("/api/register", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                username: username,
                salt: client.salt.toString(),
                verifier: v.toString()
            })
        });
        const text = await response.text();
        const resultElement = document.getElementById("registerResult");
        if (response.ok) {
            resultElement.textContent = "Registration successful! Redirecting on login...";
            resultElement.style.color = "#28a745";
            setTimeout(() => {
                window.location.href = '/login';
            }, 500);
        } else {
            const errorData = JSON.parse(text);
            resultElement.textContent = errorData.Message || "Registration failed!";
            resultElement.style.color = "#dc3545";
        }
    } catch (error) {
        document.getElementById("registerResult").textContent = "Registration failed!";
        document.getElementById("registerResult").style.color = "#dc3545";
    }
}

async function login() {
    const username = document.getElementById("loginUsername").value;
    const password = document.getElementById("loginPassword").value;
    const client = new SrpClient(password);
    try {
        const A = client.generatePublicKey();
        const startResponse = await fetch("/api/login/start", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                username: username,
                A: A.toString()
            })
        });
        const startText = await startResponse.text();
        if (!startResponse.ok) {
            throw new Error(JSON.parse(startText).Message || "Login start failed!");
        }
        const startData = JSON.parse(startText);
        const B = bigInt(startData.b || startData.B);
        const salt = startData.salt || startData.Salt;
        const sessionKey = await client.computeSessionKey(B, salt);
        const M1 = client.computeClientProof(B);

        const verifyResponse = await fetch("/api/login/verify", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ username: username, M1: A.toString() + "|" + M1.toString() })
        });
        const verifyText = await verifyResponse.text();
        if (!verifyResponse.ok) {
            throw new Error(JSON.parse(verifyText).Message || "Login verify failed!");
        }
        const verifyData = JSON.parse(verifyText);
        const M2 = bigInt(verifyData.m2 || verifyData.M2);
        if (client.verifyServerProof(M1, M2)) {

            const encryptToken = verifyData.token || verifyData.Token;
            const iv = verifyData.IV || verifyData.Iv || verifyData.iv || verifyData.iV;

            const tokenArray = await decryptStringFromBytes_Aes(base64ToUint8Array(encryptToken), sessionKey, base64ToUint8Array(iv));
            token = tokenArray.toString();

            if (!token) throw new Error("No token in response!");
            sessionStorage.setItem("jwtToken", token);
            document.getElementById("loginResult").textContent = "Login successful! Redirecting...";
            document.getElementById("loginResult").style.color = "#28a745";
            setTimeout(() => {
                window.location.href = "/donat";
            }, 500);
        } else {
            document.getElementById("loginResult").textContent = "Login is not successful!";
            document.getElementById("loginResult").style.color = "#dc3545";
        }
    } catch (error) {
        document.getElementById("loginResult").textContent = error.message || "Login failed!";
        document.getElementById("loginResult").style.color = "#dc3545";
    }
}

async function fetchWithToken(url, options = {}) {
    const token = sessionStorage.getItem("jwtToken");
    if (!token) {
        window.location.href = "/login";
        return;
    }
    options.headers = {
        ...options.headers,
        "Authorization": `Bearer ${token}`,
        "Content-Type": "application/json"
    };
    const response = await fetch(url, options);
    if (response.status === 401) {
        sessionStorage.removeItem("jwtToken");
        window.location.href = "/login";
    }
    return response;
}

async function decryptStringFromBytes_Aes(cipherText, key, iv) {
    try {
        // Convert inputs to ArrayBuffer if they are Uint8Array
        const cipherTextBuffer = cipherText instanceof Uint8Array ? cipherText.buffer : cipherText;
        const keyBuffer = key instanceof Uint8Array ? key.buffer : key;
        const ivBuffer = iv instanceof Uint8Array ? iv.buffer : iv;

        // Import the key for AES decryption
        const cryptoKey = await crypto.subtle.importKey(
            'raw',
            keyBuffer,
            { name: 'AES-CBC' },
            false,
            ['decrypt']
        );

        // Perform decryption
        const decryptedBuffer = await crypto.subtle.decrypt(
            { name: 'AES-CBC', iv: ivBuffer },
            cryptoKey,
            cipherTextBuffer
        );

        // Convert decrypted ArrayBuffer to string (assuming UTF-8 encoding)
        const decoder = new TextDecoder();
        return decoder.decode(decryptedBuffer);
    } catch (error) {
        throw new Error(`Decryption failed: ${error.message}`);
    }
}

document.addEventListener('DOMContentLoaded', () => {
    if (window.location.pathname === '/admin') {
        fetchWithToken('/admin').then(response => {
        });
    }
});