(() => {
    "use strict";

    const STORAGE_KEY = "cineReview.authToken";
    const MODAL_ID = "authModal";

    const toggleElement = (element, shouldShow) => {
        if (!element) {
            return;
        }

        element.classList.toggle("d-none", !shouldShow);
    };

    const safeInitials = value => {
        if (!value || typeof value !== "string") {
            return "CR";
        }

        const parts = value
            .replace(/\s+/g, " ")
            .trim()
            .split(" ")
            .filter(Boolean);

        if (parts.length === 0) {
            return "CR";
        }

        const first = parts[0].charAt(0);
        const lastSource = parts.length > 1 ? parts[parts.length - 1] : parts[0];
        const last = lastSource.length > 1 ? lastSource.charAt(lastSource.length - 1) : lastSource.charAt(0);

        return `${first}${last}`.toUpperCase();
    };

    document.addEventListener("DOMContentLoaded", () => {
        const root = document.querySelector("[data-auth-root]");
        if (!root) {
            return;
        }

        const loginButton = root.querySelector("[data-auth-login]");
        const skeleton = root.querySelector("[data-auth-skeleton]");
        const accountDropdown = root.querySelector("[data-auth-account]");
        const avatarImage = root.querySelector("[data-auth-avatar]");
        const initialsBadge = root.querySelector("[data-auth-initials]");
        const nameLabel = root.querySelector("[data-auth-name]");
        const emailLabel = root.querySelector("[data-auth-email]");
        const refreshButton = root.querySelector("[data-auth-refresh]");
        const profileLink = root.querySelector("[data-auth-profile]");
        const logoutButton = root.querySelector("[data-auth-logout]");
        const dropdownToggle = accountDropdown ? accountDropdown.querySelector(".account-toggle") : null;

        const baseUrlRaw = (root.dataset.identityBaseUrl || "").trim();
        const baseUrl = baseUrlRaw.replace(/\/+$/, "");

        const modalElement = document.getElementById(MODAL_ID);
        const googleButton = modalElement ? modalElement.querySelector("[data-auth-google]") : null;
        const modal = modalElement && window.bootstrap && window.bootstrap.Modal
            ? new window.bootstrap.Modal(modalElement)
            : null;

        let currentToken = null;
        let currentProfile = null;
        let currentRequest = null;

        const persistToken = token => {
            const cleanToken = typeof token === "string" ? token.trim() : "";
            currentToken = cleanToken.length > 0 ? cleanToken : null;

            if (cleanToken.length > 0) {
                localStorage.setItem(STORAGE_KEY, cleanToken);
            } else {
                localStorage.removeItem(STORAGE_KEY);
            }
        };

        const clearToken = () => {
            currentToken = null;
            currentProfile = null;
            localStorage.removeItem(STORAGE_KEY);
        };

        const setView = state => {
            switch (state) {
                case "loading":
                    toggleElement(loginButton, false);
                    toggleElement(accountDropdown, false);
                    toggleElement(skeleton, true);
                    break;
                case "ready":
                    toggleElement(loginButton, false);
                    toggleElement(skeleton, false);
                    toggleElement(accountDropdown, true);
                    break;
                default:
                    toggleElement(accountDropdown, false);
                    toggleElement(skeleton, false);
                    toggleElement(loginButton, true);
                    break;
            }
        };

        const applyProfileToUi = profile => {
            const displayName = (profile.fullName || profile.userName || profile.email || "Thành viên CineReview").trim();
            const email = profile.email || "";
            const avatar = profile.avatar || "";

            if (nameLabel) {
                nameLabel.textContent = displayName;
                nameLabel.title = displayName;
            }

            if (emailLabel) {
                emailLabel.textContent = email;
            }

            if (dropdownToggle) {
                dropdownToggle.setAttribute("aria-label", `Tài khoản ${displayName}`);
                dropdownToggle.title = displayName;
            }

            if (avatar && avatarImage) {
                avatarImage.src = avatar;
                avatarImage.alt = `Ảnh đại diện của ${displayName}`;
                avatarImage.classList.remove("d-none");
                toggleElement(initialsBadge, false);
            } else {
                if (avatarImage) {
                    avatarImage.src = "";
                    avatarImage.alt = "";
                    avatarImage.classList.add("d-none");
                }

                if (initialsBadge) {
                    initialsBadge.textContent = safeInitials(displayName);
                    toggleElement(initialsBadge, true);
                }
            }
        };

        const sanitizeUrl = () => {
            try {
                const url = new URL(window.location.href);
                const token = url.searchParams.get("token");
                if (!token) {
                    return null;
                }

                url.searchParams.delete("token");
                const cleanedSearch = url.searchParams.toString();
                const newRelative = `${url.pathname}${cleanedSearch ? `?${cleanedSearch}` : ""}${url.hash}`;
                window.history.replaceState({}, document.title, newRelative);
                return token;
            }
            catch (error) {
                console.warn("Không thể xử lý tham số token trên URL", error);
                return null;
            }
        };

        const buildRedirectUrl = () => {
            try {
                const current = new URL(window.location.href);
                current.searchParams.delete("token");
                return current.toString();
            }
            catch (error) {
                console.warn("Không thể tạo redirectClientUrl phù hợp", error);
                return window.location.origin;
            }
        };

        const fetchProfile = (token, forceRefresh = false) => {
            if (!token || typeof token !== "string") {
                return Promise.resolve(null);
            }

            if (!baseUrl) {
                console.warn("Không có cấu hình IdentityApi:BaseUrl. Bỏ qua tải thông tin tài khoản.");
                setView("signedOut");
                return Promise.resolve(null);
            }

            if (!forceRefresh && currentProfile) {
                setView("ready");
                return Promise.resolve(currentProfile);
            }

            if (currentRequest) {
                currentRequest.abort();
                currentRequest = null;
            }

            const controller = new AbortController();
            currentRequest = controller;

            setView("loading");

            return fetch(`${baseUrl}/api/Account/profile`, {
                method: "GET",
                headers: {
                    "Accept": "application/json",
                    "Authorization": `Bearer ${token}`
                },
                credentials: "include",
                referrerPolicy: "strict-origin-when-cross-origin",
                signal: controller.signal
            })
                .then(async response => {
                    if (!response.ok) {
                        const error = new Error("Không thể tải thông tin người dùng");
                        error.name = "ProfileRequestFailed";
                        error.status = response.status;
                        throw error;
                    }

                    const payload = await response.json();
                    if (!payload || payload.isSuccess !== true || !payload.data) {
                        const error = new Error(payload?.errorMessage || "Thông tin người dùng không khả dụng");
                        error.name = "ProfileInvalid";
                        throw error;
                    }

                    currentProfile = payload.data;
                    applyProfileToUi(currentProfile);
                    setView("ready");
                    return currentProfile;
                })
                .catch(error => {
                    if (controller.signal.aborted) {
                        return null;
                    }

                    const status = error?.status;
                    const unauthorized = status === 401 || status === 403;

                    console.warn("Không thể đồng bộ thông tin tài khoản", error);

                    if (unauthorized) {
                        clearToken();
                        setView("signedOut");
                    } else {
                        toggleElement(accountDropdown, false);
                        toggleElement(skeleton, false);
                        toggleElement(loginButton, true);
                    }

                    return null;
                })
                .finally(() => {
                    if (currentRequest === controller) {
                        currentRequest = null;
                    }
                });
        };

        const startSignIn = () => {
            if (!baseUrl) {
                console.error("Không tìm thấy cấu hình IdentityApi:BaseUrl. Vui lòng kiểm tra appsettings.");
                return;
            }

            const redirectUrl = encodeURIComponent(buildRedirectUrl());
            const authUrl = `${baseUrl}/api/Account/authenticate?redirectClientUrl=${redirectUrl}`;
            window.location.assign(authUrl);
        };

        const tokenFromUrl = sanitizeUrl();
        if (tokenFromUrl) {
            persistToken(tokenFromUrl);
        }

        if (!currentToken) {
            const storedToken = localStorage.getItem(STORAGE_KEY);
            if (storedToken && typeof storedToken === "string" && storedToken.length > 0) {
                persistToken(storedToken);
            }
        }

        if (currentToken) {
            fetchProfile(currentToken);
        } else {
            setView("signedOut");
        }

        if (loginButton) {
            loginButton.addEventListener("click", event => {
                event.preventDefault();
                if (modal) {
                    modal.show();
                } else {
                    startSignIn();
                }
            });
        }

        if (googleButton) {
            googleButton.addEventListener("click", event => {
                event.preventDefault();
                startSignIn();
            });
        }

        if (logoutButton) {
            logoutButton.addEventListener("click", event => {
                event.preventDefault();
                clearToken();
                if (dropdownToggle) {
                    dropdownToggle.setAttribute("aria-label", "Tài khoản CineReview");
                    dropdownToggle.title = "Tài khoản CineReview";
                }
                if (nameLabel) {
                    nameLabel.textContent = "";
                }
                if (emailLabel) {
                    emailLabel.textContent = "";
                }
                if (avatarImage) {
                    avatarImage.src = "";
                    avatarImage.alt = "";
                    avatarImage.classList.add("d-none");
                }
                if (initialsBadge) {
                    initialsBadge.textContent = "";
                }
                setView("signedOut");
            });
        }

        if (refreshButton) {
            refreshButton.addEventListener("click", event => {
                event.preventDefault();
                if (!currentToken) {
                    return;
                }

                fetchProfile(currentToken, true);
            });
        }

        if (profileLink) {
            profileLink.addEventListener("click", event => {
                // Với link, để trình duyệt điều hướng tự nhiên tới /profile
                // Nếu chưa đăng nhập, server sẽ hiện skeleton và rồi chuyển về home do thiếu token
            });
        }

        window.CineReviewAuth = {
            getToken: () => currentToken,
            getProfile: () => currentProfile,
            clear: () => {
                clearToken();
                setView("signedOut");
            },
            refresh: () => (currentToken ? fetchProfile(currentToken, true) : Promise.resolve(null))
        };
    });
})();
