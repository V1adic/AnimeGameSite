async function fetchWithToken(url, options = {}) {
    const token = sessionStorage.getItem("jwtToken");
    console.log("Sending request to:", url);
    console.log("JWT Token:", token);
    if (!token) {
        console.log("No token found, redirecting to /login");
        window.location.href = "/login";
        return;
    }
    options.headers = {
        ...options.headers,
        "Authorization": `Bearer ${token}`
    };
    console.log("Request headers:", options.headers);
    const response = await fetch(url, options);
    console.log("Response status:", response.status);
    if (response.status === 401) {
        console.log("401 Unauthorized, clearing token and redirecting to /login");
        sessionStorage.removeItem("jwtToken");
        window.location.href = "/login";
    }
    return response;
}

document.addEventListener("DOMContentLoaded", () => {
    if (window.location.pathname === "/admin") {
        const welcomeMessage = document.getElementById("welcomeMessage");
        const postSection = document.getElementById("postSection");
        const postForm = document.getElementById("postForm");
        const postResult = document.getElementById("postResult");
        const dropArea = document.getElementById("dropArea");
        const photosInput = document.getElementById("photos");

        if (!welcomeMessage || !postSection || !postForm || !postResult || !dropArea || !photosInput) {
            console.error("Required elements not found!");
            return;
        }

        // Проверяем доступ через /admin/secure (скрытая проверка)
        fetchWithToken("/admin/secure")
            .then(async response => {
                if (!response) return; // Перенаправлены на /login

                if (!response.ok) {
                    console.error("Admin page error:", response.status, await response.text());
                    welcomeMessage.textContent = "Доступ запрещён. Только для администраторов.";
                    postSection.style.display = "none";
                } else {
                    const data = await response.json();
                    console.log("Admin access granted:", data);
                    // Экранируем имя пользователя через textContent
                    const userName = DOMPurify.sanitize(data.user || "Гость");
                    welcomeMessage.textContent = `Добро пожаловать, ${userName}!`;
                    welcomeMessage.style.color = "#DED1B7";
                    postSection.style.display = "block"; // Показываем форму для админов

                    // Drag-and-drop обработчики
                    dropArea.addEventListener("dragover", (e) => {
                        e.preventDefault();
                        dropArea.style.backgroundColor = "#1E1E40";
                    });

                    dropArea.addEventListener("dragleave", () => {
                        dropArea.style.backgroundColor = "transparent";
                    });

                    dropArea.addEventListener("drop", (e) => {
                        e.preventDefault();
                        dropArea.style.backgroundColor = "transparent";
                        const files = e.dataTransfer.files;
                        handleFiles(files);
                    });

                    // Обработка вставки из буфера обмена
                    document.addEventListener("paste", (e) => {
                        const items = e.clipboardData.items;
                        const files = [];
                        for (const item of items) {
                            if (item.type.startsWith("image/")) {
                                const file = item.getAsFile();
                                files.push(file);
                            }
                        }
                        if (files.length > 0) {
                            handleFiles(files);
                        }
                    });

                    // Функция для обработки файлов
                    function handleFiles(files) {
                        const allowedTypes = ["image/jpeg", "image/png", "image/gif"];
                        const currentFiles = photosInput.files;
                        const newFiles = Array.from(files).filter(file =>
                            file.size > 0 && allowedTypes.includes(file.type)
                        );

                        if (currentFiles.length + newFiles.length > 10) {
                            postResult.textContent = "Максимум 10 изображений!";
                            postResult.style.color = "#dc3545";
                            return;
                        }

                        // Добавляем новые файлы в input
                        const dataTransfer = new DataTransfer();
                        for (const file of currentFiles) {
                            dataTransfer.items.add(file);
                        }
                        for (const file of newFiles) {
                            dataTransfer.items.add(file);
                        }
                        photosInput.files = dataTransfer.files;
                        postResult.textContent = `Добавлено ${newFiles.length} изображений. Всего: ${photosInput.files.length}`;
                        postResult.style.color = "#28a745";
                    }

                    // Обработчик отправки формы
                    postForm.addEventListener("submit", async (e) => {
                        e.preventDefault();
                        postResult.textContent = "Загрузка...";
                        postResult.style.color = "#DED1B7";

                        const formData = new FormData(postForm);
                        const photos = formData.getAll("photos");
                        const allowedTypes = ["image/jpeg", "image/png", "image/gif"];

                        if (photos.length > 10) {
                            postResult.textContent = "Максимум 10 фотографий!";
                            postResult.style.color = "#dc3545";
                            return;
                        }

                        // Проверяем типы файлов на клиенте
                        for (const photo of photos) {
                            if (photo.size > 0 && !allowedTypes.includes(photo.type)) {
                                postResult.textContent = `Файл ${photo.name} не является изображением (разрешены JPG, PNG, GIF)!`;
                                postResult.style.color = "#dc3545";
                                return;
                            }
                        }

                        try {
                            const response = await fetchWithToken("/admin/secure/post", {
                                method: "POST",
                                body: formData
                            });

                            if (!response) return; // Перенаправлены на /login

                            if (!response.ok) {
                                const errorText = await response.text();
                                console.error("Post creation error:", response.status, errorText);
                                postResult.textContent = "Не удалось создать пост!";
                                postResult.style.color = "#dc3545";
                                return;
                            }

                            const data = await response.json();
                            postResult.textContent = data.Message || "Пост успешно создан!";
                            postResult.style.color = "#28a745";
                            postForm.reset();
                            photosInput.files = new DataTransfer().files; // Очищаем input
                        } catch (error) {
                            postResult.textContent = "Ошибка при создании поста!";
                            postResult.style.color = "#dc3545";
                        }
                    });
                }
            })
            .catch(error => {
                welcomeMessage.textContent = "Ошибка загрузки данных!";
                welcomeMessage.style.color = "#dc3545";
                postSection.style.display = "none";
            });
    }
});