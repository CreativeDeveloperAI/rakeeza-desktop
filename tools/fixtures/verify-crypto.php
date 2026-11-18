<?php
// تحقّق عكسي: يفكّ بامتداد sodium في PHP ملفّ rakeeza.key وملفّ RKZF كتبهما التطبيق الجديد،
// ويطبع المفتاح الرئيسي (hex) وSHA-256 للنصّ الأصلي.
// التشغيل: php tools/fixtures/verify-crypto.php <rakeeza.key> <passphrase> <file.rkzf>

declare(strict_types=1);

[, $keyPath, $passphrase, $filePath] = $argv + [null, null, null, null];
if ($filePath === null) { fwrite(STDERR, "usage: verify-crypto.php <rakeeza.key> <passphrase> <file.rkzf>\n"); exit(2); }

$k = json_decode(file_get_contents($keyPath), true, 512, JSON_THROW_ON_ERROR);
$wrap = sodium_crypto_pwhash(32, $passphrase, base64_decode($k['salt'], true), $k['ops'], $k['mem'], SODIUM_CRYPTO_PWHASH_ALG_ARGON2ID13);
$master = sodium_crypto_secretbox_open(base64_decode($k['key'], true), base64_decode($k['nonce'], true), $wrap);
if ($master === false) { fwrite(STDERR, "wrong passphrase\n"); exit(1); }
$media = sodium_crypto_generichash('rakeeza-media', $master, 32);

$data = file_get_contents($filePath);
if (substr($data, 0, 5) !== "RKZF\x01") { fwrite(STDERR, "not RKZF\n"); exit(1); }
$chunk = unpack('N', substr($data, 5, 4))[1];
$fileNonce = substr($data, 9, 16);
$plain = '';
foreach (str_split(substr($data, 25), $chunk + 16) as $i => $c) {
    if ($c === '') { break; }
    $p = sodium_crypto_aead_xchacha20poly1305_ietf_decrypt($c, '', $fileNonce . pack('P', $i), $media);
    if ($p === false) { fwrite(STDERR, "chunk $i failed\n"); exit(1); }
    $plain .= $p;
}
echo json_encode(['masterKeyHex' => bin2hex($master), 'plainLength' => strlen($plain), 'plainSha256' => hash('sha256', $plain)]), "\n";
