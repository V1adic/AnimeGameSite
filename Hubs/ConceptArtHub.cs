using Microsoft.AspNetCore.SignalR;
using System.IO;
using System.Threading.Tasks;

namespace AnimeGameSite.Hubs
{
    public class ConceptArtHub : Hub
    {
        private readonly string _conceptArtPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "ConceptArt");
        private readonly FileSystemWatcher _watcher;
        private bool _isDisposed = false; // Отслеживание состояния хаба

        public ConceptArtHub()
        {
            if (!Directory.Exists(_conceptArtPath))
            {
                Directory.CreateDirectory(_conceptArtPath);
            }

            _watcher = new FileSystemWatcher
            {
                Path = _conceptArtPath,
                Filter = "*.*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _watcher.Changed += async (s, e) => await OnFileSystemChange();
            _watcher.Created += async (s, e) => await OnFileSystemChange();
            _watcher.Deleted += async (s, e) => await OnFileSystemChange();
            _watcher.Renamed += async (s, e) => await OnFileSystemChange();
        }

        public async Task SendConceptArts()
        {
            if (_isDisposed)
            {
                return; // Если хаб уже "расположен", ничего не делаем
            }

            if (!Directory.Exists(_conceptArtPath))
            {
                await Clients.All.SendAsync("ReceiveConceptArts", new { error = "Folder not found" });
                return;
            }

            var files = Directory.GetFiles(_conceptArtPath)
                .Select(f => new { name = Path.GetFileName(f) })
                .ToList();
            await Clients.All.SendAsync("ReceiveConceptArts", files);
        }

        private async Task OnFileSystemChange()
        {
            if (_isDisposed)
            {
                return; // Если хаб уже "расположен", ничего не делаем
            }

            if (!Directory.Exists(_conceptArtPath))
            {
                await Clients.All.SendAsync("ReceiveConceptArts", new { error = "Folder not found" });
                return;
            }

            var files = Directory.GetFiles(_conceptArtPath)
                .Select(f => new { name = Path.GetFileName(f) })
                .ToList();
            await Clients.All.SendAsync("ReceiveConceptArts", files);
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            // Вызываем Dispose при отключении клиента
            Dispose();
            return base.OnDisconnectedAsync(exception);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _watcher.EnableRaisingEvents = false; // Отключаем события
                    _watcher.Changed -= async (s, e) => await OnFileSystemChange();
                    _watcher.Created -= async (s, e) => await OnFileSystemChange();
                    _watcher.Deleted -= async (s, e) => await OnFileSystemChange();
                    _watcher.Renamed -= async (s, e) => await OnFileSystemChange();
                    _watcher.Dispose(); // Освобождаем FileSystemWatcher
                }
                _isDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}