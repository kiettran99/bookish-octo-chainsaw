(() => {
    const STORAGE_KEY = "cineReview.authToken";
    const HIDDEN_CLASS = "d-none";
    const DEFAULT_TIMEOUT = 12000;

    const root = document.querySelector("[data-profile-root]");
    if (!root) {
        return;
    }

    const dataset = root.dataset || {};
    const mode = (dataset.mode || "public").toLowerCase();
    const apiBaseUrl = normalizeBase(dataset.apiBaseUrl);
    const moviesBaseUrl = normalizeBase(dataset.moviesBaseUrl, true) || "/api/movies";
    const initialUserName = (dataset.userName || "").trim();
    const initialPage = clampPage(Number.parseInt(dataset.initialPage || "1", 10) || 1);

    const state = {
        profile: null,
        currentPage: initialPage,
        totalPages: 1,
        pageSize: 10,
        loadingReviews: false,
        lastReviewRequestId: 0,
        activeTags: [] // Cache danh sách tags để resolve tagName
    };

    const heroSection = root.querySelector("[data-profile-hero]");
    const heroSkeleton = root.querySelector("[data-profile-hero-skeleton]");
    const heroContent = root.querySelector("[data-profile-hero-content]");
    const messageContainer = root.querySelector("[data-profile-message]");
    const messageTitle = root.querySelector("[data-profile-message-title]");
    const messageBody = root.querySelector("[data-profile-message-body]");
    const messageAction = root.querySelector("[data-profile-message-action]");

    const displayNameEl = root.querySelector("[data-profile-display-name]");
    const badgeEl = root.querySelector("[data-profile-badge]");
    const bannedEl = root.querySelector("[data-profile-banned]");
    const usernameEl = root.querySelector("[data-profile-username]");
    const joinedEl = root.querySelector("[data-profile-joined]");
    const avatarImg = root.querySelector("[data-profile-avatar]");
    const initialsEl = root.querySelector("[data-profile-initials]");
    const reviewSummaryEl = root.querySelector("[data-profile-review-summary]");
    const paginationMetaEl = root.querySelector("[data-profile-pagination-meta]");

    const reviewsSection = root.querySelector("[data-profile-reviews]");
    const reviewsSkeleton = root.querySelector("[data-profile-reviews-skeleton]");
    const reviewsEmpty = root.querySelector("[data-profile-reviews-empty]");
    const reviewsGrid = root.querySelector("[data-profile-reviews-grid]");
    const paginationNav = root.querySelector("[data-profile-pagination]");
    const paginationList = root.querySelector("[data-profile-pagination-list]");
    const reviewTemplate = document.querySelector("[data-review-card-template]");


    const numberFormatter = new Intl.NumberFormat("vi-VN");
    const dateFormatter = new Intl.DateTimeFormat("vi-VN", { day: "2-digit", month: "2-digit", year: "numeric" });
    const dateTimeFormatter = new Intl.DateTimeFormat("vi-VN", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit"
    });

    function normalizeBase(value, allowRelative = false) {
        if (!value) {
            return "";
        }
        const trimmed = value.trim();
        if (!trimmed) {
            return "";
        }
        const stripped = trimmed.replace(/\/+$/, "");
        if (allowRelative && !/^https?:/i.test(stripped)) {
            return stripped.startsWith("/") ? stripped : `/${stripped}`;
        }
        return stripped;
    }

    function clampPage(value) {
        return Number.isFinite(value) && value > 0 ? value : 1;
    }

    function setHidden(element, hidden) {
        if (!element) {
            return;
        }
        element.classList.toggle(HIDDEN_CLASS, hidden);
    }

    function buildApiUrl(path) {
        const normalized = path.startsWith("/") ? path : `/${path}`;
        return apiBaseUrl ? `${apiBaseUrl}${normalized}` : normalized;
    }

    function buildMoviesUrl(path) {
        const normalized = path.startsWith("/") ? path : `/${path}`;
        return `${moviesBaseUrl}${normalized}`;
    }

    function formatNumber(value) {
        if (!Number.isFinite(value)) {
            return "0";
        }
        return numberFormatter.format(value);
    }

    function formatRating(value) {
        return Number.isFinite(value) ? value.toFixed(1).replace(/\.0$/, "") : "-";
    }

    function formatDateUtc(isoString) {
        if (!isoString) {
            return "";
        }
        const date = new Date(isoString);
        if (Number.isNaN(date.getTime())) {
            return "";
        }
        return dateFormatter.format(date);
    }

    function formatDateTimeUtc(isoString) {
        if (!isoString) {
            return "";
        }
        const date = new Date(isoString);
        if (Number.isNaN(date.getTime())) {
            return "";
        }
        return dateTimeFormatter.format(date);
    }

    function truncate(text, limit = 220) {
        if (typeof text !== "string") {
            return "";
        }
        const trimmed = text.trim();
        if (trimmed.length <= limit) {
            return trimmed;
        }
        return `${trimmed.slice(0, limit - 1).trim()}…`;
    }

    function computeBadge(score) {
        if (!Number.isFinite(score)) {
            return { label: "Reviewer Vô Danh", className: "profile-badge--secondary", show: true };
        }
        if (score < -100) {
            return { label: "Reviewer Toxic", className: "profile-badge--danger", show: true };
        }
        if (score >= -100 && score <= -10) {
            return { label: "Reviewer Chưa Công Tâm", className: "profile-badge--warning", show: true };
        }
        if (score >= -9 && score <= 10) {
            return { label: "Reviewer Vô Danh", className: "profile-badge--secondary", show: true };
        }
        if (score >= 11 && score <= 100) {
            return { label: "Reviewer Tập Sự", className: "profile-badge--info", show: true };
        }
        if (score >= 101 && score <= 500) {
            return { label: "Reviewer Có Tiếng", className: "profile-badge--primary", show: true };
        }
        return { label: "Reviewer Chuyên Nghiệp", className: "profile-badge--success", show: true };
    }

    function computeDisplayName(profile) {
        if (!profile) {
            return "";
        }
        if (profile.fullName && profile.fullName.trim().length > 0) {
            return profile.fullName.trim();
        }
        return profile.userName || "";
    }

    function computeInitials(displayName, fallback) {
        const source = displayName && displayName.trim().length > 0 ? displayName : fallback;
        if (!source) {
            return "";
        }
        const parts = source.trim().split(/\s+/).filter(Boolean);
        if (parts.length === 0) {
            return fallback.slice(0, 2).toUpperCase();
        }
        const first = parts[0][0];
        const last = parts.length > 1 ? parts[parts.length - 1][0] : parts[0][0];
        return `${(first || "").toUpperCase()}${(last || "").toUpperCase()}`.trim();
    }

    async function fetchJson(url, options = {}) {
        const { timeout = DEFAULT_TIMEOUT, signal, headers, ...rest } = options;
        const controller = new AbortController();
        const signals = [];
        if (signal) {
            signals.push(signal);
        }
        signals.push(controller.signal);
        const mergedSignal = typeof AbortSignal !== "undefined" && AbortSignal.any
            ? AbortSignal.any(signals)
            : controller.signal;

        const fetchHeaders = {
            Accept: "application/json",
            ...headers
        };

        const timer = setTimeout(() => controller.abort(), timeout);
        try {
            const response = await fetch(url, { ...rest, headers: fetchHeaders, signal: mergedSignal });
            const contentType = response.headers.get("content-type") || "";
            if (!response.ok) {
                const error = new Error(`Request failed with status ${response.status}`);
                error.status = response.status;
                if (contentType.includes("application/json")) {
                    try {
                        error.payload = await response.json();
                    } catch (_) {
                        /* ignore */
                    }
                }
                throw error;
            }
            if (contentType.includes("application/json")) {
                return await response.json();
            }
            return null;
        } finally {
            clearTimeout(timer);
        }
    }

    function ensureServiceResponse(payload) {
        if (!payload || payload.isSuccess !== true) {
            const error = new Error(payload?.errorMessage || "Không thể tải dữ liệu");
            error.isServiceError = true;
            throw error;
        }
        if (!payload.data) {
            const error = new Error("Gói dữ liệu trả về không hợp lệ");
            error.isServiceError = true;
            throw error;
        }
        return payload.data;
    }

    function showMessage(title, description, actionHref, actionText) {
        if (!messageContainer) {
            return;
        }

        if (heroSection) {
            setHidden(heroSection, true);
        }
        if (reviewsSection) {
            setHidden(reviewsSection, true);
        }
        if (overviewSection) {
            setHidden(overviewSection, true);
        }

        if (messageTitle) {
            messageTitle.textContent = title || "";
        }
        if (messageBody) {
            messageBody.textContent = description || "";
        }
        if (messageAction) {
            if (actionHref) {
                messageAction.setAttribute("href", actionHref);
            }
            if (actionText) {
                messageAction.textContent = actionText;
            }
        }

        setHidden(messageContainer, false);
    }

    function setStat(key, value) {
        if (!key) {
            return;
        }
        const elements = root.querySelectorAll(`[data-profile-stat="${key}"]`);
        elements.forEach((element) => {
            if (!element) {
                return;
            }
            if (key === "average") {
                const text = formatRating(value);
                element.textContent = text === "-" ? "-" : text;
            } else {
                element.textContent = formatNumber(value ?? 0);
            }
        });
    }

    function renderProfile(profile) {
        if (!profile) {
            return;
        }
        const displayName = computeDisplayName(profile);
        const initials = computeInitials(displayName, profile.userName || "");
        const badge = computeBadge(profile.communicationScore);

        if (displayName) {
            document.title = `${displayName} - Hồ sơ - CineReview`;
        }

        if (displayNameEl) {
            displayNameEl.textContent = displayName;
        }
        if (usernameEl) {
            usernameEl.textContent = profile.userName ? `@${profile.userName}` : "";
        }
        if (joinedEl) {
            joinedEl.textContent = profile.createdOnUtc ? `Thành viên từ ${formatDateUtc(profile.createdOnUtc)}` : "";
        }

        if (avatarImg) {
            if (profile.avatar && profile.avatar.trim().length > 0) {
                avatarImg.src = profile.avatar;
                avatarImg.alt = displayName;
                setHidden(avatarImg, false);
                setHidden(initialsEl, true);
            } else {
                avatarImg.removeAttribute("src");
                setHidden(avatarImg, true);
                if (initialsEl) {
                    initialsEl.textContent = initials;
                    setHidden(initialsEl, false);
                }
            }
        }

        if (badgeEl) {
            badgeEl.textContent = badge.label;
            badgeEl.className = `profile-badge ${badge.className}`;
            setHidden(badgeEl, !badge.show);
        }

        if (bannedEl) {
            setHidden(bannedEl, !profile.isBanned);
        }

        setStat("score", profile.communicationScore);
        setStat("total", profile.reviewStats.total);
        setStat("fair", profile.reviewStats.fair);
        setStat("unfair", profile.reviewStats.unfair);

        if (heroSkeleton) {
            setHidden(heroSkeleton, true);
        }
        if (heroContent) {
            setHidden(heroContent, false);
        }
    }


    function renderReviewSummary(profile, pageData) {
        if (reviewSummaryEl) {
            const stats = profile.reviewStats;
            reviewSummaryEl.textContent = `Tổng cộng ${formatNumber(stats.total)} review, bao gồm ${formatNumber(stats.fair)} review công tâm và ${formatNumber(stats.unfair)} review không công tâm.`;
        }
        if (paginationMetaEl) {
            if (pageData.totalPages > 1) {
                paginationMetaEl.textContent = `Trang ${pageData.page} trên ${pageData.totalPages}`;
                setHidden(paginationMetaEl, false);
            } else {
                setHidden(paginationMetaEl, true);
            }
        }
    }

    function mapProfilePayload(payload) {
        const stats = payload.reviewStats || {};
        return {
            id: payload.id,
            userName: payload.userName,
            fullName: payload.fullName,
            avatar: payload.avatar,
            createdOnUtc: payload.createdOnUtc,
            isBanned: Boolean(payload.isBanned),
            communicationScore: payload.communicationScore ?? 0,
            reviewStats: {
                total: stats.totalReviews ?? 0,
                fair: stats.fairReviews ?? 0,
                unfair: stats.unfairReviews ?? 0
            }
        };
    }

    function mapReviewPayload(item) {
        return {
            id: item.id,
            movieId: item.tmdbMovieId,
            rating: item.rating,
            status: item.status,
            type: item.type,
            description: item.description,
            descriptionTag: item.descriptionTag,
            createdOnUtc: item.createdOnUtc,
            updatedOnUtc: item.updatedOnUtc,
            communicationScore: item.communicationScore,
            rejectReason: item.rejectReason
        };
    }

    async function loadActiveTags() {
        if (state.activeTags.length > 0) {
            return state.activeTags;
        }
        try {
            const url = buildApiUrl("/api/tag/active");
            const payload = await fetchJson(url, { timeout: 10000 });
            const data = ensureServiceResponse(payload);
            state.activeTags = Array.isArray(data) ? data : [];
            return state.activeTags;
        } catch (error) {
            console.warn("Không thể tải danh sách tags", error);
            return [];
        }
    }

    function parseTags(raw) {
        if (!raw) {
            return [];
        }
        let value = raw;
        if (typeof raw === "string") {
            try {
                value = JSON.parse(raw);
            } catch (_) {
                return [];
            }
        }
        if (!Array.isArray(value)) {
            return [];
        }
        return value
            .map((item) => ({
                id: Number(item?.tagId),
                name: typeof item?.tagName === "string" ? item.tagName : null,
                rating: Number(item?.rating)
            }))
            .filter((item) => Number.isFinite(item.id) && item.id > 0);
    }

    function resolveTagNames(tags, activeTags) {
        if (!Array.isArray(tags) || tags.length === 0) {
            return [];
        }
        if (!Array.isArray(activeTags) || activeTags.length === 0) {
            return tags.map(tag => ({
                ...tag,
                name: tag.name || `Tag #${tag.id}`
            }));
        }

        const tagMap = new Map();
        activeTags.forEach(tag => {
            if (tag && Number.isFinite(tag.id)) {
                tagMap.set(tag.id, tag.name || `Tag #${tag.id}`);
            }
        });

        return tags.map(tag => ({
            ...tag,
            name: tag.name || tagMap.get(tag.id) || `Tag #${tag.id}`
        }));
    }

    function calculateTagAverage(tags) {
        if (!Array.isArray(tags) || tags.length === 0) {
            return null;
        }
        const validRatings = tags.map((tag) => Number(tag.rating)).filter((value) => Number.isFinite(value));
        if (validRatings.length === 0) {
            return null;
        }
        const sum = validRatings.reduce((total, current) => total + current, 0);
        return sum / validRatings.length;
    }

    /**
     * Render rating bar with 5 cells (each cell = 2 points on 1-10 scale)
     * @param {number} rating - Rating value from 1-10
     * @returns {string} HTML markup for rating bar
     */
    function renderRatingBar(rating) {
        const normalized = Math.max(0, Math.min(10, Number(rating) || 0));
        const totalCells = 5;

        const cellsHtml = Array.from({ length: totalCells }, (_, index) => {
            const cellStart = index * 2;
            const valueWithinCell = Math.min(Math.max(normalized - cellStart, 0), 2);
            const fillPercent = Math.max(0, Math.min(100, (valueWithinCell / 2) * 100));
            const isFilled = fillPercent >= 99;
            const hasFill = fillPercent > 0;

            const cellClasses = ["rating-bar__cell"];
            if (isFilled) cellClasses.push("rating-bar__cell--filled");
            if (hasFill && !isFilled) cellClasses.push("rating-bar__cell--partial");

            return `
                <span class="${cellClasses.join(" ")}">
                    <span class="rating-bar__fill" style="width: ${fillPercent}%;"></span>
                </span>
            `;
        }).join("");

        return `<div class="rating-bar">${cellsHtml}</div>`;
    }

    function getReviewStatusMeta(status) {
        switch (status) {
            case 1:
                return { label: "Đã duyệt", className: "profile-review-card__status--released" };
            case 2:
                return { label: "Từ chối", className: "profile-review-card__status--deleted" };
            default:
                return { label: "Chờ duyệt", className: "profile-review-card__status--pending" };
        }
    }

    function getReviewTypeLabel(type) {
        return type === 0 ? "Review theo Tag" : "Review tự do";
    }

    function buildMetadataItems(review) {
        const items = [];
        items.push({ label: "Loại", value: getReviewTypeLabel(review.type) });
        if (review.createdOnUtc) {
            items.push({ label: "Gửi", value: formatDateTimeUtc(review.createdOnUtc) });
        }
        if (Number.isFinite(review.communicationScore)) {
            items.push({ label: "Điểm cộng đồng", value: formatNumber(review.communicationScore) });
        }
        return items;
    }

    function renderReviews(page, reviews, movieLookup) {
        if (!reviewsGrid || !reviewTemplate) {
            return;
        }

        reviewsGrid.innerHTML = "";
        const fragment = document.createDocumentFragment();

        reviews.forEach((review) => {
            const clone = reviewTemplate.content.firstElementChild.cloneNode(true);
            const posterImage = clone.querySelector("[data-review-poster-image]");
            const posterFallback = clone.querySelector("[data-review-poster-fallback]");
            const titleEl = clone.querySelector("[data-review-movie-title]");
            const metaEl = clone.querySelector("[data-review-meta]");
            const statusEl = clone.querySelector("[data-review-status]");
            const descriptionEl = clone.querySelector("[data-review-description]");
            const tagsEl = clone.querySelector("[data-review-tags]");
            const ratingEl = clone.querySelector("[data-review-rating]");
            const timestampEl = clone.querySelector("[data-review-timestamp]");
            const parsedTags = parseTags(review.descriptionTag);
            const tags = resolveTagNames(parsedTags, state.activeTags);

            // Thêm sự kiện click để chuyển trang chi tiết review
            // Chỉ điều hướng khi người dùng click vào vùng nền/non-interactive của card.
            clone.style.cursor = "pointer";
            clone.addEventListener("click", (e) => {
                try {
                    // Nếu sự kiện đã bị preventDefault hoặc là nhấp chuột không phải nút trái
                    if (e.defaultPrevented) return;
                    if (e.button !== 0) return; // chỉ xử lý left-click

                    // Nếu người dùng giữ phím Ctrl/Meta/Shift/Alt thì giữ hành vi mặc định (ví dụ mở tab mới)
                    if (e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;

                    // Các selector tương tác: nếu click nằm trong các phần tử này thì KHÔNG điều hướng
                    const interactiveSelector = [
                        'a',
                        'button',
                        'input',
                        'textarea',
                        'select',
                        'label',
                        '[role="button"]',
                        '[role="link"]',
                        '[data-review-poster-image]',
                        '[data-review-poster-fallback]',
                        '.profile-review-card__tag-metric',
                        '.profile-review-card__tag-name',
                        '.profile-review-card__tag-score',
                        '.rating-bar__cell',
                        '.rating-bar__fill',
                        '[data-review-meta]',
                        '[data-review-description]',
                        '[data-review-reject-reason]',
                        '[data-review-status]',
                        '[data-review-rating]',
                        '[data-review-timestamp]',
                        '.btn',
                        '.badge',
                        'svg',
                        'path'
                    ].join(',');

                    if (e.target && e.target.closest && e.target.closest(interactiveSelector)) {
                        return;
                    }

                    // Nếu không phải interactive area thì điều hướng tới trang phim
                    if (Number.isInteger(review.movieId) && review.movieId > 0) {
                        window.location.href = `/movies/${review.movieId}#community-reviews`;
                    }
                } catch (err) {
                    // An toàn: nếu có lỗi trong handler, không làm đứt trải nghiệm
                    console.error('Error handling review card click', err);
                }
            });

            const movie = movieLookup.get(review.movieId) || null;
            if (movie && posterImage) {
                posterImage.src = movie.posterUrl;
                posterImage.alt = `Poster ${movie.title}`;
                setHidden(posterImage, false);
                if (posterFallback) {
                    setHidden(posterFallback, true);
                }
            } else if (posterFallback) {
                posterFallback.textContent = review.movieId ? String(review.movieId) : "N/A";
                setHidden(posterFallback, false);
                if (posterImage) {
                    setHidden(posterImage, true);
                }
            }

            if (titleEl) {
                titleEl.textContent = movie ? movie.title : `TMDB #${review.movieId}`;
            }

            if (metaEl) {
                metaEl.innerHTML = "";
                buildMetadataItems(review).forEach((item) => {
                    const metaItem = document.createElement("span");
                    const label = document.createElement("strong");
                    label.textContent = `${item.label}:`;
                    metaItem.appendChild(label);
                    metaItem.appendChild(document.createTextNode(` ${item.value}`));
                    metaEl.appendChild(metaItem);
                });
            }

            if (statusEl) {
                if (review.type === 1) {
                    const statusMeta = getReviewStatusMeta(review.status);
                    statusEl.textContent = statusMeta.label;
                    statusEl.className = `profile-review-card__status ${statusMeta.className}`;
                    statusEl.style.display = "";
                } else {
                    statusEl.style.display = "none";
                }
            }

            // Hiển thị reject reason nếu review bị từ chối (status = 2)
            const rejectReasonEl = clone.querySelector("[data-review-reject-reason]");
            if (rejectReasonEl) {
                const hasRejectReason = review.status === 2 && typeof review.rejectReason === "string" && review.rejectReason.trim().length > 0;
                if (hasRejectReason) {
                    const reasonText = rejectReasonEl.querySelector("[data-reject-reason-text]");
                    if (reasonText) {
                        reasonText.textContent = review.rejectReason;
                    }
                    setHidden(rejectReasonEl, false);
                } else {
                    setHidden(rejectReasonEl, true);
                }
            }

            if (descriptionEl) {
                const hasDescription = typeof review.description === "string" && review.description.trim().length > 0;
                if (hasDescription) {
                    // Thêm class đặc biệt cho freeform review để nhấn mạnh nội dung
                    if (review.type === 1) {
                        descriptionEl.classList.add("profile-review-card__freeform-text");
                    }
                    descriptionEl.textContent = truncate(review.description, 320);
                    setHidden(descriptionEl, false);
                } else {
                    setHidden(descriptionEl, true);
                }
            }

            if (tagsEl) {
                tagsEl.innerHTML = "";
                if (tags.length === 0) {
                    setHidden(tagsEl, true);
                } else {
                    setHidden(tagsEl, false);

                    const tagsContainer = document.createElement("div");
                    tagsContainer.className = "profile-review-card__tags-container";

                    tags.forEach((tag) => {
                        const tagItem = document.createElement("div");
                        tagItem.className = "profile-review-card__tag-metric";

                        const tagHeader = document.createElement("div");
                        tagHeader.className = "profile-review-card__tag-header";

                        const tagName = document.createElement("span");
                        tagName.className = "profile-review-card__tag-name";
                        tagName.innerHTML = `<i class="bi bi-tag-fill me-2"></i>${tag.name}`;

                        const tagScore = document.createElement("span");
                        tagScore.className = "profile-review-card__tag-score";
                        tagScore.textContent = Number.isFinite(tag.rating) ? `${tag.rating}/10` : "-";

                        tagHeader.appendChild(tagName);
                        tagHeader.appendChild(tagScore);
                        tagItem.appendChild(tagHeader);

                        if (Number.isFinite(tag.rating)) {
                            const ratingBar = document.createElement("div");
                            ratingBar.className = "profile-review-card__tag-bar";
                            ratingBar.innerHTML = renderRatingBar(tag.rating);
                            tagItem.appendChild(ratingBar);
                        }

                        tagsContainer.appendChild(tagItem);
                    });

                    tagsEl.appendChild(tagsContainer);
                }
            }

            if (ratingEl) {
                const ratingSource = review.type === 0 ? calculateTagAverage(tags) : Number(review.rating);
                const ratingText = formatRating(ratingSource);
                ratingEl.textContent = ratingText === "-" ? "Điểm: -" : `Điểm: ${ratingText}/10`;
            }

            if (timestampEl) {
                if (review.updatedOnUtc) {
                    timestampEl.textContent = `Cập nhật ${formatDateTimeUtc(review.updatedOnUtc)}`;
                } else {
                    setHidden(timestampEl, true);
                }
            }

            fragment.appendChild(clone);
        });

        reviewsGrid.appendChild(fragment);

        const hasReviews = reviews.length > 0;
        setHidden(reviewsGrid, !hasReviews);
        setHidden(reviewsEmpty, hasReviews);
        setHidden(reviewsSkeleton, true);

        renderPagination(page, state.totalPages);
    }

    function renderPagination(page, totalPages) {
        if (!paginationNav || !paginationList) {
            return;
        }
        if (totalPages <= 1) {
            setHidden(paginationNav, true);
            paginationList.innerHTML = "";
            return;
        }

        paginationList.innerHTML = "";

        const addPageItem = (label, targetPage, options = {}) => {
            const { disabled = false, active = false } = options;
            const li = document.createElement("li");
            li.className = "page-item";
            if (disabled) {
                li.classList.add("disabled");
            }
            if (active) {
                li.classList.add("active");
            }
            const link = document.createElement("a");
            link.className = "page-link";
            link.href = "#";
            link.textContent = label;
            link.addEventListener("click", (event) => {
                event.preventDefault();
                if (disabled || active || state.loadingReviews) {
                    return;
                }
                goToPage(targetPage);
            });
            li.appendChild(link);
            paginationList.appendChild(li);
        };

        addPageItem("Trước", page - 1, { disabled: page <= 1 });

        const windowSize = 5;
        let start = Math.max(1, page - 2);
        let end = Math.min(totalPages, start + windowSize - 1);
        if (end - start < windowSize - 1) {
            start = Math.max(1, end - windowSize + 1);
        }

        if (start > 1) {
            addPageItem("1", 1, { active: page === 1 });
            if (start > 2) {
                addEllipsis();
            }
        }

        for (let i = start; i <= end; i += 1) {
            addPageItem(String(i), i, { active: page === i });
        }

        if (end < totalPages) {
            if (end < totalPages - 1) {
                addEllipsis();
            }
            addPageItem(String(totalPages), totalPages, { active: page === totalPages });
        }

        addPageItem("Sau", page + 1, { disabled: page >= totalPages });
        setHidden(paginationNav, false);

        function addEllipsis() {
            const li = document.createElement("li");
            li.className = "page-item disabled";
            const span = document.createElement("span");
            span.className = "page-link";
            span.textContent = "…";
            li.appendChild(span);
            paginationList.appendChild(li);
        }
    }

    function updatePageInUrl(page) {
        const normalized = clampPage(page);
        const url = new URL(window.location.href);
        if (normalized <= 1) {
            url.searchParams.delete("page");
        } else {
            url.searchParams.set("page", String(normalized));
        }
        const next = url.toString();
        if (next !== window.location.href) {
            window.history.replaceState({}, document.title, next);
        }
    }

    async function goToPage(page) {
        const normalized = clampPage(page);
        if (normalized === state.currentPage) {
            return;
        }
        state.currentPage = normalized;
        updatePageInUrl(normalized);
        await loadReviewsPage(normalized);
    }

    async function fetchMovieSummaries(movieIds, token) {
        if (!Array.isArray(movieIds) || movieIds.length === 0) {
            return new Map();
        }
        const uniqueIds = Array.from(new Set(movieIds.filter((id) => Number.isInteger(id) && id > 0)));
        if (uniqueIds.length === 0) {
            return new Map();
        }
        const query = encodeURIComponent(uniqueIds.join(","));
        const url = buildMoviesUrl(`/summaries?ids=${query}`);
        try {
            const response = await fetchJson(url, {
                headers: token
                    ? { Authorization: `Bearer ${token}` }
                    : undefined,
                timeout: 15000
            });
            const entries = Array.isArray(response?.items) ? response.items : [];
            const map = new Map();
            entries.forEach((item) => {
                if (!item || !Number.isInteger(item.id)) {
                    return;
                }
                map.set(item.id, {
                    id: item.id,
                    title: item.title || `TMDB #${item.id}`,
                    posterUrl: item.posterUrl || "",
                    releaseDate: item.releaseDate || null,
                    communityScore: item.communityScore,
                    isNowPlaying: Boolean(item.isNowPlaying)
                });
            });
            return map;
        } catch (error) {
            console.warn("Không thể tải thông tin phim", error);
            return new Map();
        }
    }

    async function loadReviewsPage(page, token) {
        if (!state.profile || !reviewsSection) {
            return;
        }
        if (!apiBaseUrl) {
            return;
        }
        setHidden(reviewsSection, false);
        setHidden(reviewsSkeleton, false);
        setHidden(reviewsGrid, true);
        setHidden(reviewsEmpty, true);
        setHidden(paginationNav, true);

        state.loadingReviews = true;
        const requestId = ++state.lastReviewRequestId;

        try {
            const url = buildApiUrl(`/api/review/my-reviews?page=${page}&pageSize=${state.pageSize}`);
            const headers = token ? { Authorization: `Bearer ${token}` } : undefined;
            const payload = await fetchJson(url, { headers });
            if (requestId !== state.lastReviewRequestId) {
                return;
            }
            const data = ensureServiceResponse(payload);
            const items = Array.isArray(data.items) ? data.items.map(mapReviewPayload) : [];
            const mapped = items;
            const pageNumber = clampPage(data.page || page);
            const totalCount = Number.isFinite(data.totalCount) ? data.totalCount : mapped.length;
            const pageSize = Number.isFinite(data.pageSize) ? data.pageSize : state.pageSize;
            const totalPages = data.totalPages && data.totalPages > 0
                ? data.totalPages
                : Math.max(1, Math.ceil(totalCount / (pageSize || 1)));

            state.currentPage = pageNumber;
            state.totalPages = totalPages;
            state.pageSize = pageSize;

            const movieIds = mapped.map((item) => item.movieId);
            const movieLookup = await fetchMovieSummaries(movieIds, token);

            // Load active tags để resolve tag names
            await loadActiveTags();

            renderReviewSummary(state.profile, { page: pageNumber, totalPages });
            renderReviews({ page: pageNumber, totalPages }, mapped, movieLookup);
        } catch (error) {
            if (error.name === "AbortError") {
                return;
            }
            console.error("Không thể tải danh sách review", error);
            setHidden(reviewsSkeleton, true);
            const message = document.createElement("div");
            message.className = "alert alert-warning mb-0";
            message.role = "alert";
            message.textContent = "Không thể tải danh sách review của bạn. Vui lòng thử lại sau.";
            reviewsGrid.innerHTML = "";
            reviewsGrid.appendChild(message);
            setHidden(reviewsGrid, false);
        } finally {
            if (requestId === state.lastReviewRequestId) {
                state.loadingReviews = false;
            }
        }
    }

    async function loadSelfProfile() {
        if (!apiBaseUrl) {
            showMessage("Thiếu cấu hình", "Không thể tải hồ sơ vì thiếu cấu hình API.");
            return;
        }

        const token = window.localStorage.getItem(STORAGE_KEY);
        if (!token) {
            showMessage("Bạn chưa đăng nhập", "Hãy đăng nhập để xem hồ sơ của bạn.", "/", "Về trang chủ");
            return;
        }

        try {
            const profileResponse = await fetchJson(buildApiUrl("/api/user/profile"), {
                headers: {
                    Authorization: `Bearer ${token}`
                },
                credentials: "include"
            });
            const profilePayload = ensureServiceResponse(profileResponse);
            const profile = mapProfilePayload(profilePayload);
            state.profile = profile;
            renderProfile(profile);
            renderReviewSummary(profile, { page: state.currentPage, totalPages: 1 });
            await loadReviewsPage(state.currentPage, token);
        } catch (error) {
            handleProfileError(error, {
                notFound: "Không thể tìm thấy hồ sơ của bạn.",
                unauthorized: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                general: "Không thể tải hồ sơ. Vui lòng thử lại sau."
            });
        }
    }

    async function loadPublicProfile(userName) {
        if (!apiBaseUrl) {
            showMessage("Thiếu cấu hình", "Không thể tải hồ sơ vì thiếu cấu hình API.");
            return;
        }

        if (!userName) {
            showMessage("Thiếu thông tin", "Chúng tôi không xác định được người dùng bạn muốn xem.");
            return;
        }

        try {
            const profileResponse = await fetchJson(buildApiUrl(`/api/user/${encodeURIComponent(userName)}`));
            const profilePayload = ensureServiceResponse(profileResponse);
            const profile = mapProfilePayload(profilePayload);
            state.profile = profile;
            renderProfile(profile);
        } catch (error) {
            handleProfileError(error, {
                notFound: `Không tìm thấy hồ sơ cho người dùng ${userName}.`,
                unauthorized: "Không thể truy cập hồ sơ này.",
                general: "Không thể tải hồ sơ. Vui lòng thử lại sau."
            });
        }
    }

    function handleProfileError(error, messages) {
        console.error("Profile error", error);
        if (error.status === 404) {
            showMessage("Không tìm thấy", messages.notFound || "Không tìm thấy hồ sơ.");
            return;
        }
        if (error.status === 401 || error.status === 403) {
            showMessage("Cần đăng nhập", messages.unauthorized || "Bạn cần đăng nhập để xem nội dung này.");
            return;
        }
        if (error.name === "AbortError") {
            showMessage("Hết thời gian", "Yêu cầu mất quá nhiều thời gian. Vui lòng thử lại.");
            return;
        }
        showMessage("Có lỗi xảy ra", messages.general || "Không thể tải dữ liệu. Vui lòng thử lại.");
    }

    function init() {
        setHidden(messageContainer, true);
        if (heroSkeleton) {
            setHidden(heroSkeleton, false);
        }
        if (heroContent) {
            setHidden(heroContent, true);
        }

        if (mode === "self") {
            loadSelfProfile();
        } else {
            loadPublicProfile(initialUserName);
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
