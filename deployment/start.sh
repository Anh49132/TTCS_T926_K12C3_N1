#!/bin/sh
set -eu

fail() { printf '%s\n' "$1" >&2; exit 1; }

[ "${ASPNETCORE_ENVIRONMENT:-}" = "Production" ] || fail 'Ứng dụng online phải chạy ở Production.'
jwt_secret=${Jwt__SecretKey:-}
[ "${#jwt_secret}" -ge 32 ] || fail 'Cần cấu hình Jwt__SecretKey riêng, ít nhất 32 ký tự.'
case "$jwt_secret" in
    CHANGE_ME*) fail 'Cần thay khóa JWT mẫu bằng secret riêng trên hosting.' ;;
esac
[ "${RAILWAY_VOLUME_MOUNT_PATH:-}" = "/data" ] || fail 'Cần gắn Railway Volume tại /data trước khi chạy.'
[ -s "$DatabasePath" ] || fail 'Thiếu database trên volume. Không tự tạo hoặc ghi đè database.'
[ -n "${PasswordReset__PublicBaseUrl:-}" ] || fail 'Thiếu PasswordReset__PublicBaseUrl.'
case "$PasswordReset__PublicBaseUrl" in
    https://?*) ;;
    *) fail 'PasswordReset__PublicBaseUrl phải dùng HTTPS.' ;;
esac
[ -n "${PasswordReset__From:-}" ] || fail 'Thiếu email người gửi đã xác minh.'
case "${PasswordReset__Provider:-Smtp}" in
    Brevo|brevo|BREVO) [ -n "${Brevo__ApiKey:-}" ] || fail 'Thiếu Brevo__ApiKey.' ;;
    Smtp|smtp|SMTP) [ -n "${PasswordReset__Host:-}" ] || fail 'Thiếu SMTP host.' ;;
    *) fail 'Nhà cung cấp email không hợp lệ.' ;;
esac
[ -z "${PasswordReset__PickupDirectory:-}" ] || fail 'Không dùng email pickup trong Production.'

mkdir -p "$IdentityImagePath" "$RoomImagesPath" "$DataProtectionKeysPath"
# Read-only schema validation; never initialize or upgrade at container startup.
dotnet QL_PhongTro.dll --check-database
exec dotnet QL_PhongTro.dll --urls "http://0.0.0.0:${PORT:-8080}"
