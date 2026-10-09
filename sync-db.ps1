# sync-db.ps1 - Chép DB local lên Render (ghi đè toàn bộ dữ liệu trên Render)
$RenderUrl = $env:RENDER_DB_URL
if (-not $RenderUrl) {
    Write-Host "Chua dat RENDER_DB_URL. Chay truoc: `$env:RENDER_DB_URL = 'postgresql://...'" -ForegroundColor Red
    exit 1
}

Remove-Item .\hotel.dump -ErrorAction SilentlyContinue

Write-Host "1/3 Dump DB local..."
docker run --rm -e PGPASSWORD=local123 -v "${PWD}:/backup" postgres:18 `
  pg_dump -h host.docker.internal -p 5433 -U postgres -d hotelblazor --no-owner --no-acl -Fc -f /backup/hotel.dump
if ($LASTEXITCODE -ne 0) { Write-Host "Dump loi" -ForegroundColor Red; exit 1 }

Write-Host "2/3 Restore len Render..."
docker run --rm -v "${PWD}:/backup" postgres:18 `
  pg_restore -d "$RenderUrl" --no-owner --no-acl --clean --if-exists /backup/hotel.dump
if ($LASTEXITCODE -ne 0) { Write-Host "Restore co loi, kiem tra log o tren" -ForegroundColor Yellow }

Write-Host "3/3 Don dep..."
Remove-Item .\hotel.dump -ErrorAction SilentlyContinue

Write-Host "Xong. Vao Render bam Restart service de app ket noi lai." -ForegroundColor Green

# PS D:\FileBaiTap\Ky7\3tc\DevOps\Code\HotelBlazor> $env:RENDER_DB_URL = "postgresql://hotel_db_2upt_user:qv3sUDcDgqKZ5h1fZwX3EMUDOGntuH3g@dpg-daud1h7lot8c73alu7ug-a.singapore-postgres.render.com/hotel_db_2upt?sslmode=require"
# PS D:\FileBaiTap\Ky7\3tc\DevOps\Code\HotelBlazor> .\sync-db.ps1