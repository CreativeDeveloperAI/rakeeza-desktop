<?php
// يولّد متّجهات اختبار للتشفير بامتداد sodium في PHP، أي التنفيذ نفسه الذي يعتمد عليه النظام القديم.
// هذا تنفيذ مستقلّ للمواصفة المكتوبة في البرومت، وليس شيفرة Laravel الأصلية،
// فهو يثبت التوافق مع libsodium عبر PHP، ولا يغني عن fixtures حقيقية في docs/legacy/fixtures/.
//
// التشغيل: php tools/fixtures/make-crypto-fixtures.php tests/Rakeeza.Core.Tests/Fixtures/php-sodium

declare(strict_types=1);

$out = $argv[1] ?? die("usage: make-crypto-fixtures.php <out-dir>\n");
@mkdir($out, 0777, true);

$passphrase = 'ركيزة-تجريبية-2026';
$ops = 2;
$mem = 67108864; // 64 MiB

$master = random_bytes(SODIUM_CRYPTO_SECRETBOX_KEYBYTES);
$salt = random_bytes(SODIUM_CRYPTO_PWHASH_SALTBYTES);
$nonce = random_bytes(SODIUM_CRYPTO_SECRETBOX_NONCEBYTES);
$wrap = sodium_crypto_pwhash(32, $passphrase, $salt, $ops, $mem, SODIUM_CRYPTO_PWHASH_ALG_ARGON2ID13);
$keyFile = [
    'version' => 1,
    'kdf' => 'argon2id',
    'salt' => base64_encode($salt),
    'ops' => $ops,
    'mem' => $mem,
    'nonce' => base64_encode($nonce),
    'key' => base64_encode(sodium_crypto_secretbox($master, $nonce, $wrap)),
];
file_put_contents("$out/rakeeza.key", json_encode($keyFile));

$media = sodium_crypto_generichash('rakeeza-media', $master, 32);

function plain(int $length): string {
    $s = '';
    for ($i = 0; $i < $length; $i++) { $s .= chr(($i * 31 + 7) % 256); }
    return $s;
}

function rkzf(string $plain, string $key, int $chunkSize): string {
    $fileNonce = random_bytes(16);
    $out = "RKZF\x01" . pack('N', $chunkSize) . $fileNonce;
    foreach (str_split($plain, $chunkSize) as $i => $chunk) {
        if ($chunk === '') { break; }
        $nonce = $fileNonce . pack('P', $i);
        $out .= sodium_crypto_aead_xchacha20poly1305_ietf_encrypt($chunk, '', $nonce, $key);
    }
    return $out;
}

$files = [];
foreach ([['single.rkzf', 1000, 1048576], ['chunked.rkzf', 200, 64], ['exact.rkzf', 128, 64]] as [$name, $length, $chunk]) {
    $p = plain($length);
    file_put_contents("$out/$name", rkzf($p, $media, $chunk));
    $files[] = ['name' => $name, 'chunkSize' => $chunk, 'plainLength' => $length, 'plainSha256' => hash('sha256', $p)];
}

file_put_contents("$out/expected.json", json_encode([
    'generator' => 'php ' . PHP_VERSION . ', libsodium ' . SODIUM_LIBRARY_VERSION,
    'passphrase' => $passphrase,
    'masterKeyHex' => bin2hex($master),
    'mediaKeyHex' => bin2hex($media),
    'files' => $files,
], JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES) . "\n");
echo "written to $out\n";
