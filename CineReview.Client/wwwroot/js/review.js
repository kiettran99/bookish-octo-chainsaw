(() => {
    "use strict";

    /**
     * Review System with Choices.js dropdown, null-safe error handling
     * Fixed data structure: {tagId, rating} only
     */

    const REVIEW_MODE = {
        TEMPLATE: "template",
        FREEFORM: "freeform"
    };

    const REVIEW_TYPE = {
        TAG: 0,
        FREEFORM: 1
    };

    const REVIEW_RATING_TYPE = {
        FAIR: 0,
        UNFAIR: 1
    };

    const REVIEWS_PER_PAGE = 5;

    const REVIEW_STATUS = {
        PENDING: 0,
        RELEASED: 1,
        DELETED: 2
    };

    const REVIEW_STATUS_LABELS = {
        [REVIEW_STATUS.PENDING]: "Chờ duyệt",
        [REVIEW_STATUS.RELEASED]: "Đã duyệt",
        [REVIEW_STATUS.DELETED]: "Đã xóa"
    };

    // Helper functions
    const toggleElement = (element, shouldShow) => {
        if (!element) return;
        element.classList.toggle("d-none", !shouldShow);
        if (shouldShow) {
            element.removeAttribute("hidden");
            element.setAttribute("aria-hidden", "false");
        } else {
            element.setAttribute("hidden", "");
            element.setAttribute("aria-hidden", "true");
        }
    };

    const formatDate = dateString => {
        try {
            const date = new Date(dateString);
            const now = new Date();
            const diffMs = now - date;
            const diffMins = Math.floor(diffMs / 60000);
            const diffHours = Math.floor(diffMs / 3600000);
            const diffDays = Math.floor(diffMs / 86400000);

            if (diffMins < 60) return `${diffMins} phút trước`;
            if (diffHours < 24) return `${diffHours} giờ trước`;
            if (diffDays < 7) return `${diffDays} ngày trước`;

            return date.toLocaleDateString("vi-VN", {
                year: "numeric",
                month: "long",
                day: "numeric"
            });
        } catch {
            return dateString;
        }
    };

    const safeInitials = name => {
        if (!name || typeof name !== "string") return "U";
        const parts = name.trim().split(" ").filter(Boolean);
        if (parts.length === 0) return "U";
        const first = parts[0].charAt(0);
        const last = parts.length > 1 ? parts[parts.length - 1].charAt(0) : first;
        return `${first}${last}`.toUpperCase();
    };

    const escapeHtml = text => {
        if (!text) return "";
        const div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    };

    const parseNumberOrNull = value => {
        const numericValue = Number(value);
        return Number.isFinite(numericValue) ? numericValue : null;
    };

    const formatDateTime = value => {
        if (!value) return "Không xác định";
        try {
            return new Date(value).toLocaleString("vi-VN", {
                year: "numeric",
                month: "2-digit",
                day: "2-digit",
                hour: "2-digit",
                minute: "2-digit"
            });
        } catch {
            return value;
        }
    };

    /**
     * Render rating bar with 5 cells (each cell = 2 points on 1-10 scale)
     * @param {number} rating - Rating value from 1-10
     * @returns {string} HTML markup for rating bar
     */
    const renderRatingBar = rating => {
        const normalized = Math.max(0, Math.min(10, parseNumberOrNull(rating) ?? 0));
        const totalCells = 5;

        const cellsHtml = Array.from({ length: totalCells }, (_, index) => {
            const cellStart = index * 2;
            const valueWithinCell = Math.min(Math.max(normalized - cellStart, 0), 2);
            const fillPercent = Math.max(0, Math.min(100, (valueWithinCell / 2) * 100));
            const isFilled = fillPercent >= 99;
            const hasFill = fillPercent > 0;

            const cellClasses = ["rating-bar__cell"];
            if (isFilled) cellClasses.push("rating-bar__cell--full");
            if (hasFill && !isFilled) cellClasses.push("rating-bar__cell--partial");

            return `
                <span class="${cellClasses.join(" ")}">
                    <span class="rating-bar__fill" style="width: ${fillPercent}%;"></span>
                </span>
            `;
        }).join("");

        return `<div class="rating-bar">${cellsHtml}</div>`;
    };

    /**
     * Format rating value to string with up to 1 decimal place
     * @param {number} value
     * @returns {string | null}
     */
    const formatRatingValue = value => {
        if (!Number.isFinite(value)) return null;
        const rounded = Math.round(value * 10) / 10;
        return Number.isInteger(rounded) ? `${rounded}` : rounded.toFixed(1);
    };

    /**
     * Calculate average rating from tag list
     * @param {Array} tags - Array of tag items with rating
     * @returns {number} Average rating
     */
    const calculateAverageRating = tags => {
        if (!Array.isArray(tags) || tags.length === 0) return 0;

        const validRatings = tags
            .map(item => parseNumberOrNull(item?.rating))
            .filter(r => r !== null && r > 0);

        if (validRatings.length === 0) return 0;

        const sum = validRatings.reduce((acc, val) => acc + val, 0);
        return Math.round((sum / validRatings.length) * 10) / 10; // Round to 1 decimal
    };

    const getStatusBadgeClass = status => {
        if (status === REVIEW_STATUS.RELEASED) return "bg-success";
        if (status === REVIEW_STATUS.PENDING) return "bg-warning text-dark";
        return "bg-secondary";
    };

    document.addEventListener("DOMContentLoaded", () => {
        const root = document.querySelector("[data-review-sheet-root]");
        if (!root) return;

        const movieIdElement = document.querySelector("[data-movie-id]");
        const movieId = movieIdElement ? parseInt(movieIdElement.dataset.movieId) : null;

        if (!movieId) {
            console.warn("Không tìm thấy movie ID");
            return;
        }

        const apiBaseUrlRaw = (root.dataset.apiBaseUrl || "").trim();
        const apiBaseUrl = apiBaseUrlRaw.replace(/\/+$/, "");

        if (!apiBaseUrl) {
            console.error("Không có cấu hình CineReviewApi:BaseUrl");
            return;
        }

        // Elements - with null checks
        const layer = root.querySelector("[data-review-layer]");
        const closeButtons = root.querySelectorAll("[data-review-close]");
        const modeButtons = root.querySelectorAll("[data-review-mode]");
        const tagSelect = root.querySelector("[data-tag-select]");
        const selectedTagsContainer = root.querySelector("[data-selected-tags-container]");
        const freeformTextarea = root.querySelector("[data-review-freeform]");
        const freeformRatingContainer = root.querySelector("[data-freeform-rating]");
        const submitButton = root.querySelector("[data-review-submit]");
        const loader = root.querySelector("[data-review-loader]");
        const templateSections = root.querySelectorAll("[data-review-template-section]");
        const freeformSection = root.querySelector("[data-review-freeform-section]");
        const confirmDialog = root.querySelector("[data-review-confirm]");
        const confirmCancel = root.querySelector("[data-review-confirm-cancel]");
        const confirmSend = root.querySelector("[data-review-confirm-send]");
        const summaryTemplateList = root.querySelector("[data-review-summary-template]");
        const summaryFreeformText = root.querySelector("[data-review-summary-freeform]");
        const statusDialog = root.querySelector("[data-review-status]");
        const statusIcon = root.querySelector("[data-review-status-icon]");
        const statusTitle = root.querySelector("[data-review-status-title]");
        const statusDescription = root.querySelector("[data-review-status-description]");
        const statusSuccessActions = root.querySelector("[data-review-status-success]");
        const statusFailureActions = root.querySelector("[data-review-status-failure]");
        const startAnotherButton = root.querySelector("[data-review-start-another]");
        const closeCompleteButton = root.querySelector("[data-review-close-complete]");
        const retryButton = root.querySelector("[data-review-retry]");
        const dismissStatusButton = root.querySelector("[data-review-dismiss-status]");
        const writeReviewButtons = document.querySelectorAll("[data-write-review]");
        const reviewsRoot = document.querySelector("[data-review-base-url]");
        const reviewsContainer = document.querySelector("[data-reviews-container]");
        const paginationContainer = document.querySelector("[data-review-pagination]");
        const reviewsEmptyState = document.querySelector("[data-reviews-empty]");
        const templateExistingContainer = root.querySelector("[data-review-template-existing]");
        const templateFormContainer = root.querySelector("[data-review-template-form]");
        const freeformExistingContainer = root.querySelector("[data-review-freeform-existing]");
        const freeformFormContainer = root.querySelector("[data-review-freeform-form]");

        // State
        let currentMode = REVIEW_MODE.TEMPLATE;
        let currentReviewData = null;
        let activeTags = [];
        let selectedTagRatings = {}; // {tagId: rating}
        let freeformRating = 5;
        let choicesInstance = null;
        let isSubmitting = false; // Prevent double submission
        const fallbackBasePath = window.location.pathname
            .replace(/\/binh-luan\/trang-\d+$/i, "")
            .replace(/\/page-\d+$/i, "");
        const reviewBaseUrl = reviewsRoot ? (reviewsRoot.dataset.reviewBaseUrl || "").replace(/\/+$/, "") : fallbackBasePath.replace(/\/+$/, "");
        let currentReviewPage = reviewsRoot ? parseInt(reviewsRoot.dataset.reviewInitialPage || "1", 10) || 1 : 1;
        let userReviewState = { tag: false, freeform: false, count: 0 };
        let userReviewDetails = { tag: null, freeform: null };
        const reviewRatingState = new Map();

        const resolveTagNameById = (tagId) => {
            if (!Number.isFinite(tagId)) return null;
            const matchedTag = activeTags.find(tag => Number(tag?.id) === tagId);
            if (!matchedTag) return null;
            const name = typeof matchedTag.name === "string" ? matchedTag.name.trim() : null;
            return name && name.length > 0 ? name : null;
        };

        const buildReviewMetaSection = (detail, reviewType) => {
            // For TAG type, calculate average from tags; for FREEFORM use detail.rating
            let effectiveRating = detail?.rating;
            if (reviewType === REVIEW_TYPE.TAG && Array.isArray(detail?.tags)) {
                effectiveRating = calculateAverageRating(detail.tags);
            }

            const formattedRating = formatRatingValue(effectiveRating);
            const ratingDisplay = formattedRating ? `${formattedRating}/10` : "Không xác định";
            const fairVotesDisplay = Number.isFinite(detail?.fairVotes) ? detail.fairVotes.toString() : "0";
            const unfairVotesDisplay = Number.isFinite(detail?.unfairVotes) ? detail.unfairVotes.toString() : "0";
            const supportDisplay = typeof detail?.supportScoreLabel === "string" && detail.supportScoreLabel.length > 0
                ? detail.supportScoreLabel
                : "0.0%";
            const createdAtDisplay = typeof detail?.createdAtText === "string" && detail.createdAtText.length > 0
                ? detail.createdAtText
                : "Không xác định";
            const updatedAtDisplay = typeof detail?.updatedAtText === "string" && detail.updatedAtText.length > 0
                ? detail.updatedAtText
                : null;

            const updatedMarkup = updatedAtDisplay
                ? `
                    <div class="your-review-card__meta-item">
                        <span class="your-review-card__meta-label"><i class="bi bi-arrow-counterclockwise me-1"></i>Cập nhật</span>
                        <span class="your-review-card__meta-value">${escapeHtml(updatedAtDisplay)}</span>
                    </div>
                `
                : "";

            // Hiển thị Công tâm, Không công tâm, Điểm ủng hộ khi:
            // - Review type là TAG (0): Luôn hiển thị
            // - Review type là FREEFORM (1): Chỉ hiển thị khi Status = Released (1)
            const isReleased = detail?.status === REVIEW_STATUS.RELEASED;
            const isTag = reviewType === REVIEW_TYPE.TAG;
            const isFreeform = reviewType === REVIEW_TYPE.FREEFORM;
            const showVotingStats = isTag || (isFreeform && isReleased);

            const votingStatsMarkup = showVotingStats
                ? `
                    <div class="your-review-card__meta-item">
                        <span class="your-review-card__meta-label"><i class="bi bi-hand-thumbs-up me-1"></i>Công tâm</span>
                        <span class="your-review-card__meta-value">${escapeHtml(fairVotesDisplay)}</span>
                    </div>
                    <div class="your-review-card__meta-item">
                        <span class="your-review-card__meta-label"><i class="bi bi-hand-thumbs-down me-1"></i>Không công tâm</span>
                        <span class="your-review-card__meta-value">${escapeHtml(unfairVotesDisplay)}</span>
                    </div>
                    <div class="your-review-card__meta-item">
                        <span class="your-review-card__meta-label"><i class="bi bi-activity me-1"></i>Điểm ủng hộ</span>
                        <span class="your-review-card__meta-value">${escapeHtml(supportDisplay)}</span>
                    </div>
                `
                : "";

            return `
                <div class="your-review-card__meta">
                    <div class="your-review-card__meta-item">
                        <span class="your-review-card__meta-label"><i class="bi bi-star-fill me-1 text-warning"></i>Điểm đánh giá</span>
                        <span class="your-review-card__meta-value">${escapeHtml(ratingDisplay)}</span>
                    </div>
                    ${votingStatsMarkup}
                    <div class="your-review-card__meta-item">
                        <span class="your-review-card__meta-label"><i class="bi bi-clock-history me-1"></i>Ngày tạo</span>
                        <span class="your-review-card__meta-value">${escapeHtml(createdAtDisplay)}</span>
                    </div>
                    ${updatedMarkup}
                </div>
            `;
        };

        const buildTagListMarkup = (tags) => {
            if (!Array.isArray(tags) || tags.length === 0) {
                return '<p class="text-secondary mb-0">Không có tag nào trong review này.</p>';
            }

            const itemsMarkup = tags
                .map(item => {
                    if (!item) return "";
                    const labelSource = typeof item.tagName === "string" && item.tagName.trim().length > 0
                        ? item.tagName.trim()
                        : resolveTagNameById(item.tagId) || (Number.isFinite(item.tagId) ? `Tag #${item.tagId}` : "Tag");
                    const ratingValue = parseNumberOrNull(item.rating);
                    const ratingBar = renderRatingBar(ratingValue);
                    const ratingLabel = formatRatingValue(ratingValue) ?? "0";

                    return `
                        <li class="tag-rating-item">
                            <div class="tag-rating-item__header">
                                <span class="tag-rating-item__name">
                                    <i class="bi bi-tag-fill me-2"></i>${escapeHtml(labelSource)}
                                </span>
                                <span class="tag-rating-item__score">${ratingLabel}/10</span>
                            </div>
                            <div class="tag-rating-item__bar">
                                ${ratingBar}
                            </div>
                        </li>
                    `;
                })
                .filter(Boolean)
                .join("");

            return itemsMarkup
                ? `<ul class="tag-rating-list">${itemsMarkup}</ul>`
                : '<p class="text-secondary mb-0">Không có tag nào trong review này.</p>';
        };

        const buildReviewCardMarkup = (detail, config) => {
            if (!detail) return "";

            const typeLabel = typeof config?.label === "string" ? config.label : "Review";
            const typeIcon = typeof config?.icon === "string" ? config.icon : "bi-chat-text";
            const bodyMarkup = typeof config?.body === "string" && config.body.trim().length > 0
                ? config.body
                : '<p class="mb-0 text-secondary">Không có nội dung cho review này.</p>';
            const statusLabel = typeof detail.statusLabel === "string" ? detail.statusLabel : "Không xác định";
            const statusClass = detail.statusBadgeClass || "bg-secondary";
            const reviewType = detail.type;

            // Chỉ hiển thị status badge cho FREEFORM type (review tự do)
            const showStatusBadge = reviewType === REVIEW_TYPE.FREEFORM;
            const statusBadgeMarkup = showStatusBadge
                ? `<span class="badge ${statusClass}">${escapeHtml(statusLabel)}</span>`
                : "";

            return `
                <article class="your-review-card">
                    <header class="your-review-card__header">
                        <div class="d-flex align-items-center gap-2 flex-wrap">
                            <span class="badge badge-surface-info"><i class="bi ${typeIcon} me-1"></i>${escapeHtml(typeLabel)}</span>
                            ${statusBadgeMarkup}
                        </div>
                    </header>
                    <div class="your-review-card__body">
                        ${bodyMarkup}
                    </div>
                    ${buildReviewMetaSection(detail, reviewType)}
                </article>
            `;
        };

        const buildTagReviewMarkup = (detail) => {
            if (!detail) return "";

            const descriptionBlock = typeof detail.description === "string" && detail.description.trim().length > 0
                ? `<p class="mb-0">${escapeHtml(detail.description.trim()).replace(/\n/g, "<br>")}</p>`
                : "";

            const bodyParts = [buildTagListMarkup(detail.tags)];
            if (descriptionBlock) {
                bodyParts.push(descriptionBlock);
            }

            return buildReviewCardMarkup(detail, {
                label: "Review theo tag",
                icon: "bi-tags-fill",
                body: bodyParts.join("\n")
            });
        };

        const buildFreeformReviewMarkup = (detail) => {
            if (!detail) return "";

            const content = typeof detail.description === "string" && detail.description.trim().length > 0
                ? `<p class="mb-0">${escapeHtml(detail.description.trim()).replace(/\n/g, "<br>")}</p>`
                : '<p class="mb-0 text-secondary">Bạn đã gửi review không có nội dung chi tiết.</p>';

            return buildReviewCardMarkup(detail, {
                label: "Review chi tiết",
                icon: "bi-pencil-square",
                body: content
            });
        };

        const renderExistingReviewCards = () => {
            const hasTagReview = Boolean(userReviewDetails.tag);
            const hasFreeformReview = Boolean(userReviewDetails.freeform);
            const isTemplateMode = currentMode === REVIEW_MODE.TEMPLATE;
            const isFreeformMode = currentMode === REVIEW_MODE.FREEFORM;

            if (templateExistingContainer) {
                if (hasTagReview) {
                    templateExistingContainer.innerHTML = buildTagReviewMarkup(userReviewDetails.tag);
                } else {
                    templateExistingContainer.innerHTML = "";
                }

                const shouldShowTagCard = hasTagReview && isTemplateMode;
                toggleElement(templateExistingContainer, shouldShowTagCard);
            }

            if (templateFormContainer) {
                const shouldShowForm = !hasTagReview || !isTemplateMode;
                toggleElement(templateFormContainer, shouldShowForm);
            }

            if (freeformExistingContainer) {
                if (hasFreeformReview) {
                    freeformExistingContainer.innerHTML = buildFreeformReviewMarkup(userReviewDetails.freeform);
                } else {
                    freeformExistingContainer.innerHTML = "";
                }

                const shouldShowFreeformCard = hasFreeformReview && isFreeformMode;
                toggleElement(freeformExistingContainer, shouldShowFreeformCard);
            }

            if (freeformFormContainer) {
                const shouldShowFreeformForm = !hasFreeformReview || !isFreeformMode;
                toggleElement(freeformFormContainer, shouldShowFreeformForm);
            }
        };

        const normalizeReviewDetail = (review) => {
            if (!review) return null;

            const ratingValue = parseNumberOrNull(review.rating);
            const supportScoreValue = parseNumberOrNull(review.communicationScore);
            const fairVotesValue = parseNumberOrNull(review.fairVotes);
            const unfairVotesValue = parseNumberOrNull(review.unfairVotes);

            const detail = {
                type: review.type,
                rating: ratingValue !== null ? ratingValue : null,
                status: review.status,
                statusLabel: REVIEW_STATUS_LABELS[review.status] || "Không xác định",
                statusBadgeClass: getStatusBadgeClass(review.status),
                createdAtText: formatDateTime(review.createdOnUtc),
                updatedAtText: review.updatedOnUtc ? formatDateTime(review.updatedOnUtc) : null,
                supportScoreLabel: supportScoreValue !== null ? `${supportScoreValue.toFixed(1)}%` : null,
                fairVotes: fairVotesValue !== null ? fairVotesValue : null,
                unfairVotes: unfairVotesValue !== null ? unfairVotesValue : null,
                description: typeof review.description === "string" ? review.description : "",
                tags: []
            };

            if (review.type === REVIEW_TYPE.TAG && Array.isArray(review.descriptionTag)) {
                detail.tags = review.descriptionTag
                    .map(item => {
                        if (!item) return null;
                        if (typeof item === "object") {
                            const itemTagId = parseNumberOrNull(item.tagId);
                            const itemTagName = typeof item.tagName === "string" && item.tagName.trim().length > 0
                                ? item.tagName.trim()
                                : null;
                            const itemRating = parseNumberOrNull(item.rating);
                            return {
                                tagId: itemTagId,
                                tagName: itemTagName,
                                rating: itemRating
                            };
                        }

                        if (typeof item === "string" && item.trim().length > 0) {
                            return {
                                tagId: null,
                                tagName: item.trim(),
                                rating: null
                            };
                        }

                        return null;
                    })
                    .filter(Boolean);
            }

            return detail;
        };

        const updateExistingReviewDetails = (reviews) => {
            const safeReviews = Array.isArray(reviews) ? reviews.filter(Boolean) : [];
            const validReviews = safeReviews.filter(review => review.status !== REVIEW_STATUS.DELETED);

            const selectLatestByType = (type) => {
                const matches = validReviews.filter(review => review.type === type);
                if (matches.length === 0) return null;

                matches.sort((a, b) => {
                    const left = new Date(a?.updatedOnUtc || a?.createdOnUtc || 0).getTime();
                    const right = new Date(b?.updatedOnUtc || b?.createdOnUtc || 0).getTime();
                    return right - left;
                });

                return matches[0];
            };

            const latestTagReview = selectLatestByType(REVIEW_TYPE.TAG);
            const latestFreeformReview = selectLatestByType(REVIEW_TYPE.FREEFORM);

            userReviewDetails = {
                tag: latestTagReview ? normalizeReviewDetail(latestTagReview) : null,
                freeform: latestFreeformReview ? normalizeReviewDetail(latestFreeformReview) : null
            };

            renderExistingReviewCards();
        };

        const getEffectiveBaseUrl = () => (reviewBaseUrl ? reviewBaseUrl : "/");

        const buildReviewUrl = (page) => {
            const targetPage = Number.isFinite(page) && page > 1 ? Math.floor(page) : 1;
            if (targetPage <= 1) {
                return getEffectiveBaseUrl();
            }
            return `${getEffectiveBaseUrl()}/page-${targetPage}`;
        };

        const updateModeAvailability = () => {
            const templateCompleted = Boolean(userReviewDetails.tag);
            const freeformCompleted = Boolean(userReviewDetails.freeform);

            modeButtons.forEach(btn => {
                const mode = btn.dataset.reviewMode;
                const isTemplate = mode === REVIEW_MODE.TEMPLATE;
                const isFreeform = mode === REVIEW_MODE.FREEFORM;
                const isCompleted = (isTemplate && templateCompleted) || (isFreeform && freeformCompleted);
                if (!btn.dataset.reviewLabel) {
                    btn.dataset.reviewLabel = (btn.textContent || "").trim();
                }
                const baseLabel = btn.dataset.reviewLabel || btn.textContent || "";

                btn.disabled = false;
                btn.classList.remove("is-disabled");
                btn.classList.toggle("is-complete", isCompleted);

                if (isCompleted) {
                    btn.setAttribute("data-review-status", "complete");
                    btn.setAttribute("title", "Bạn đã gửi review này. Xem chi tiết ở bên dưới.");
                    if (baseLabel) {
                        btn.setAttribute("aria-label", `${baseLabel} (đã gửi)`);
                    }
                } else {
                    btn.removeAttribute("data-review-status");
                    btn.removeAttribute("title");
                    if (baseLabel) {
                        btn.setAttribute("aria-label", baseLabel);
                    } else {
                        btn.removeAttribute("aria-label");
                    }
                }
            });

            if (selectedTagsContainer) {
                selectedTagsContainer.dataset.disabled = templateCompleted ? "true" : "false";
            }

            if (freeformTextarea) {
                freeformTextarea.disabled = freeformCompleted;
                if (freeformCompleted) {
                    freeformTextarea.value = "";
                    freeformTextarea.placeholder = "Bạn đã hoàn thành review chi tiết cho phim này. Nội dung review đang hiển thị ở trên.";
                } else {
                    freeformTextarea.placeholder = "Hãy kể rõ trải nghiệm rạp, cảm xúc sau khi xem…";
                }
            }

            const slider = freeformRatingContainer ? freeformRatingContainer.querySelector("#freeformRating") : null;
            if (slider) {
                slider.disabled = freeformCompleted;
            }
        };

        const enforceAvailableMode = () => {
            const templateCompleted = Boolean(userReviewDetails.tag);
            const freeformCompleted = Boolean(userReviewDetails.freeform);

            if (currentMode === REVIEW_MODE.TEMPLATE && templateCompleted && !freeformCompleted) {
                switchMode(REVIEW_MODE.FREEFORM, true);
            }

            if (currentMode === REVIEW_MODE.FREEFORM && freeformCompleted && !templateCompleted) {
                switchMode(REVIEW_MODE.TEMPLATE, true);
            }
        };

        const applyUserReviewState = (reviews) => {
            const safeReviews = Array.isArray(reviews) ? reviews : [];
            userReviewState = {
                tag: safeReviews.some(review => review?.type === REVIEW_TYPE.TAG && review?.status !== REVIEW_STATUS.DELETED),
                freeform: safeReviews.some(review => review?.type === REVIEW_TYPE.FREEFORM && review?.status !== REVIEW_STATUS.DELETED),
                count: safeReviews.filter(review => review?.status !== REVIEW_STATUS.DELETED).length
            };

            updateExistingReviewDetails(safeReviews);

            updateModeAvailability();
            enforceAvailableMode();

            if (userReviewState.tag) {
                selectedTagRatings = {};
                renderSelectedTags();
            }

            if (userReviewState.freeform) {
                freeformRating = 5;
                renderFreeformRating();
            }

            updateSubmitButton();
        };

        // Auth helpers
        const getAuthToken = () => {
            if (window.CineReviewAuth && typeof window.CineReviewAuth.getToken === "function") {
                return window.CineReviewAuth.getToken();
            }
            return null;
        };

        const isLoggedIn = () => {
            const token = getAuthToken();
            return token !== null && token !== "";
        };

        const getCurrentUserId = () => {
            const token = getAuthToken();
            if (!token) return null;

            try {
                // Decode JWT token to get userId
                const parts = token.split('.');
                if (parts.length !== 3) return null;

                const payload = JSON.parse(atob(parts[1]));
                // JWT claim name for userId in this system is "id"
                const userId = payload.id || payload.sub || payload.nameid || payload.userId;

                if (userId) {
                    const parsedId = parseInt(userId);
                    return Number.isFinite(parsedId) ? parsedId : null;
                }
                return null;
            } catch (error) {
                console.error("Error decoding token:", error);
                return null;
            }
        };

        const showLoginPrompt = () => {
            const authModal = document.getElementById("authModal");
            if (authModal && window.bootstrap && window.bootstrap.Modal) {
                const modal = new window.bootstrap.Modal(authModal);
                modal.show();
            } else {
                alert("Vui lòng đăng nhập để viết review");
            }
        };

        // Load active tags from API
        const loadActiveTags = async () => {
            try {
                const response = await fetch(`${apiBaseUrl}/api/Tag/active`, {
                    method: "GET",
                    headers: { "Accept": "application/json" }
                });

                if (!response.ok) throw new Error("Không thể tải tags");

                const result = await response.json();
                if (!result.isSuccess || !result.data) throw new Error("Dữ liệu không hợp lệ");

                activeTags = result.data;
                initializeTagsDropdown();
                renderExistingReviewCards();
            } catch (error) {
                console.error("Lỗi khi tải tags:", error);
            }
        };

        // Initialize Choices.js dropdown
        const initializeTagsDropdown = () => {
            if (!tagSelect || !window.Choices) return;

            // Group tags by category for optgroups
            const groups = {};
            activeTags.forEach(tag => {
                const category = tag.categoryName || "Khác";
                if (!groups[category]) {
                    groups[category] = [];
                }
                groups[category].push({
                    value: tag.id.toString(),
                    label: tag.name,
                    customProperties: { tag: tag }
                });
            });

            // Build choices array with groups
            const choices = [];
            Object.keys(groups).forEach(category => {
                choices.push({
                    label: category,
                    id: category,
                    disabled: false,
                    choices: groups[category]
                });
            });

            // Initialize Choices
            choicesInstance = new Choices(tagSelect, {
                removeItemButton: true,
                searchEnabled: true,
                searchPlaceholderValue: "Tìm kiếm tag...",
                noResultsText: "Không tìm thấy",
                itemSelectText: "Click để chọn",
                placeholderValue: "Chọn tags...",
                choices: choices
            });

            // Listen to selection changes
            tagSelect.addEventListener("change", handleTagSelectionChange);
        };

        // Handle tag selection change
        const handleTagSelectionChange = () => {
            if (!choicesInstance) return;

            const selectedValues = choicesInstance.getValue(true);

            // Update selectedTagRatings: add new, remove deselected
            const newRatings = {};
            selectedValues.forEach(tagId => {
                const id = parseInt(tagId);
                newRatings[id] = selectedTagRatings[id] || 5; // Keep existing rating or default
            });
            selectedTagRatings = newRatings;

            renderSelectedTags();
            updateSubmitButton();
        };

        // Render selected tags with rating sliders
        const renderSelectedTags = () => {
            if (!selectedTagsContainer) return;

            selectedTagsContainer.innerHTML = "";

            if (userReviewState.tag) {
                selectedTagsContainer.innerHTML = '<div class="alert alert-info mb-0"><i class="bi bi-info-circle me-2"></i>Bạn đã hoàn thành review dạng tag cho phim này. Nội dung review đang hiển thị ở trên.</div>';
                return;
            }

            const selectedIds = Object.keys(selectedTagRatings);
            if (selectedIds.length === 0) {
                selectedTagsContainer.innerHTML = '<p class="text-secondary">Chưa chọn tag nào. Chọn ít nhất 1 tag để tiếp tục.</p>';
                return;
            }

            const title = document.createElement("div");
            title.className = "mb-3";
            title.innerHTML = '<h6 class="text-white">Đánh giá điểm cho từng tag:</h6>';
            selectedTagsContainer.appendChild(title);

            selectedIds.forEach(tagIdStr => {
                const tagId = parseInt(tagIdStr);
                const tag = activeTags.find(t => t.id === tagId);
                if (!tag) return;

                const card = document.createElement("div");
                card.className = "tag-rating-card";

                card.innerHTML = `
                    <div class="tag-rating-card__info">
                        <div class="tag-rating-card__name">${escapeHtml(tag.name)}</div>
                        <div class="tag-rating-card__category">${escapeHtml(tag.categoryName || "")}</div>
                    </div>
                    <div class="tag-rating-card__control">
                        <input type="range" 
                               class="form-range tag-rating-slider" 
                               min="1" 
                               max="10" 
                               value="${selectedTagRatings[tagId]}"
                               data-tag-id="${tagId}">
                        <span class="tag-rating-value">${selectedTagRatings[tagId]}/10</span>
                    </div>
                `;

                const slider = card.querySelector("input[type=range]");
                const valueDisplay = card.querySelector(".tag-rating-value");

                if (slider && valueDisplay) {
                    slider.addEventListener("input", (e) => {
                        const rating = parseInt(e.target.value);
                        selectedTagRatings[tagId] = rating;
                        valueDisplay.textContent = `${rating}/10`;
                    });
                }

                selectedTagsContainer.appendChild(card);
            });
        };

        // Render freeform rating
        const renderFreeformRating = () => {
            if (!freeformRatingContainer) return;

            freeformRatingContainer.innerHTML = `
                <label class="form-label" for="freeformRating">Điểm đánh giá tổng thể</label>
                <div class="d-flex align-items-center gap-3">
                    <input type="range" class="form-range flex-grow-1" id="freeformRating" min="1" max="10" value="${freeformRating}">
                    <span class="badge bg-info fs-6" id="freeformRatingValue">${freeformRating}/10</span>
                </div>
                <div class="review-sheet__hint">Cho điểm từ 1 (tệ) đến 10 (xuất sắc)</div>
            `;

            const slider = freeformRatingContainer.querySelector("#freeformRating");
            const badge = freeformRatingContainer.querySelector("#freeformRatingValue");

            if (slider && badge) {
                slider.addEventListener("input", (e) => {
                    freeformRating = parseInt(e.target.value);
                    badge.textContent = `${freeformRating}/10`;
                });

                slider.disabled = userReviewState.freeform;
            }

            if (userReviewState.freeform) {
                const notice = document.createElement("div");
                notice.className = "alert alert-info mt-3 mb-0";
                notice.innerHTML = '<i class="bi bi-info-circle me-2"></i>Bạn đã hoàn thành review chi tiết cho phim này. Nội dung review đang hiển thị ở trên.';
                freeformRatingContainer.appendChild(notice);
            }
        };

        // Open/Close sheet
        const openReviewSheet = () => {
            if (!isLoggedIn()) {
                showLoginPrompt();
                return;
            }

            if (layer) {
                layer.removeAttribute("hidden");
                document.body.style.overflow = "hidden";
            }

            renderExistingReviewCards();
            updateSubmitButton();
        };

        const closeReviewSheet = () => {
            if (layer) {
                layer.setAttribute("hidden", "");
                document.body.style.overflow = "";
            }
            resetForm();
        };

        // Reset form
        const resetForm = () => {
            selectedTagRatings = {};
            freeformRating = 5;
            isSubmitting = false;

            if (choicesInstance) {
                choicesInstance.removeActiveItems();
            }
            if (freeformTextarea) {
                freeformTextarea.value = "";
            }

            renderSelectedTags();
            renderFreeformRating();
            // Switch to the first available mode
            const defaultMode = !userReviewState.tag ? REVIEW_MODE.TEMPLATE :
                !userReviewState.freeform ? REVIEW_MODE.FREEFORM : REVIEW_MODE.TEMPLATE;
            switchMode(defaultMode, true);
            updateSubmitButton();
        };

        // Switch mode
        const switchMode = (mode, _isInternal = false) => {
            if (mode !== REVIEW_MODE.TEMPLATE && mode !== REVIEW_MODE.FREEFORM) {
                return;
            }

            currentMode = mode;

            modeButtons.forEach(btn => {
                const isActive = btn.dataset.reviewMode === mode;
                btn.classList.toggle("is-active", isActive);
                btn.setAttribute("aria-selected", isActive);
            });

            const isTemplate = mode === REVIEW_MODE.TEMPLATE;
            templateSections.forEach(section => toggleElement(section, isTemplate));
            toggleElement(freeformSection, !isTemplate);

            renderExistingReviewCards();

            updateSubmitButton();
        };

        // Update submit button
        const updateSubmitButton = () => {
            if (!submitButton) return;

            let isValid = false;
            if (currentMode === REVIEW_MODE.TEMPLATE) {
                isValid = Object.keys(selectedTagRatings).length > 0;
            } else {
                const content = freeformTextarea ? freeformTextarea.value.trim() : "";
                isValid = content.length >= 10;
            }

            if ((currentMode === REVIEW_MODE.TEMPLATE && userReviewState.tag) ||
                (currentMode === REVIEW_MODE.FREEFORM && userReviewState.freeform) ||
                userReviewState.count >= 2) {
                isValid = false;
            }

            submitButton.disabled = !isValid || isSubmitting;
        };

        // Show confirm dialog
        const showConfirmDialog = () => {
            if (!confirmDialog) return;

            if ((currentMode === REVIEW_MODE.TEMPLATE && userReviewState.tag) ||
                (currentMode === REVIEW_MODE.FREEFORM && userReviewState.freeform)) {
                return;
            }

            if (currentMode === REVIEW_MODE.TEMPLATE) {
                // Build tag ratings array - ONLY tagId and rating (no tagName)
                const tagRatingsArray = Object.entries(selectedTagRatings).map(([tagIdStr, rating]) => ({
                    tagId: parseInt(tagIdStr),
                    rating: rating
                }));

                // Render summary with tag names for display
                if (summaryTemplateList) {
                    summaryTemplateList.innerHTML = "";
                    tagRatingsArray.forEach(item => {
                        const tag = activeTags.find(t => t.id === item.tagId);
                        const li = document.createElement("li");
                        li.innerHTML = `<span>${escapeHtml(tag ? tag.name : "Unknown")}</span><span class="badge bg-info">${item.rating}/10</span>`;
                        summaryTemplateList.appendChild(li);
                    });
                }

                toggleElement(summaryTemplateList, true);
                toggleElement(summaryFreeformText, false);

                const avgRaw = tagRatingsArray.reduce((sum, item) => sum + item.rating, 0) / tagRatingsArray.length;
                const avgRating = Math.round(avgRaw * 10) / 10;

                currentReviewData = {
                    tmdbMovieId: movieId,
                    type: REVIEW_TYPE.TAG,
                    descriptionTag: tagRatingsArray, // Only {tagId, rating}
                    description: null,
                    rating: avgRating
                };
            } else {
                const content = freeformTextarea ? freeformTextarea.value.trim() : "";

                if (summaryFreeformText) {
                    summaryFreeformText.innerHTML = `
                        <div class="mb-2"><strong>Nội dung:</strong></div>
                        <div class="text-secondary mb-3">${escapeHtml(content)}</div>
                        <div><strong>Điểm đánh giá:</strong> <span class="badge bg-info">${freeformRating}/10</span></div>
                    `;
                }

                toggleElement(summaryTemplateList, false);
                toggleElement(summaryFreeformText, true);

                currentReviewData = {
                    tmdbMovieId: movieId,
                    type: REVIEW_TYPE.FREEFORM,
                    descriptionTag: null,
                    description: content,
                    rating: freeformRating
                };
            }

            confirmDialog.removeAttribute("hidden");
        };

        const hideConfirmDialog = () => {
            if (confirmDialog) {
                confirmDialog.setAttribute("hidden", "");
            }
        };

        // Submit review
        const submitReview = async () => {
            if (!currentReviewData || isSubmitting) return;

            isSubmitting = true;
            hideConfirmDialog();

            // Show loader
            if (loader) {
                toggleElement(loader, true);
            }
            if (submitButton) {
                submitButton.disabled = true;
            }

            try {
                const token = getAuthToken();
                if (!token) {
                    throw new Error("Không tìm thấy token xác thực");
                }

                const response = await fetch(`${apiBaseUrl}/api/Review`, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "Authorization": `Bearer ${token}`,
                        "Accept": "application/json"
                    },
                    body: JSON.stringify(currentReviewData)
                });

                let result;
                try {
                    result = await response.json();
                } catch (parseError) {
                    throw new Error("Lỗi xử lý phản hồi từ server");
                }

                if (!response.ok || !result.isSuccess) {
                    throw new Error(result.message || result.errorMessage || "Không thể gửi review");
                }

                // Success
                const isTagBased = currentReviewData.type === 0;
                const message = isTagBased
                    ? "Review của bạn đã được gửi và hiển thị ngay!"
                    : "Review của bạn đã được gửi. Chúng tôi sẽ duyệt trong thời gian sớm nhất.";

                showStatusDialog(true, message);

                setTimeout(() => {
                    loadMovieReviews();
                    if (window.CineReviewButton && typeof window.CineReviewButton.checkUserReview === "function") {
                        window.CineReviewButton.checkUserReview();
                    }
                }, 1000);

            } catch (error) {
                console.error("Lỗi khi gửi review:", error);
                showStatusDialog(false, error.message || "Đã xảy ra lỗi. Vui lòng thử lại.");
            } finally {
                isSubmitting = false;
                if (loader) {
                    toggleElement(loader, false);
                }
                if (submitButton) {
                    submitButton.disabled = false;
                }
            }
        };

        // Status dialog
        const showStatusDialog = (isSuccess, message) => {
            if (!statusDialog) return;

            if (isSuccess) {
                if (statusIcon) {
                    statusIcon.className = "review-dialog__status-icon bi bi-check-circle-fill text-success";
                }
                if (statusTitle) {
                    statusTitle.textContent = "Gửi thành công!";
                }
                toggleElement(statusSuccessActions, true);
                toggleElement(statusFailureActions, false);
            } else {
                if (statusIcon) {
                    statusIcon.className = "review-dialog__status-icon bi bi-exclamation-circle-fill text-danger";
                }
                if (statusTitle) {
                    statusTitle.textContent = "Không thể gửi review";
                }
                toggleElement(statusSuccessActions, false);
                toggleElement(statusFailureActions, true);
            }

            if (statusDescription) {
                statusDescription.textContent = message;
            }

            statusDialog.removeAttribute("hidden");
        };

        const hideStatusDialog = () => {
            if (statusDialog) {
                statusDialog.setAttribute("hidden", "");
            }
        };

        // Load batch ratings for reviews
        const loadBatchRatings = async (reviewIds) => {
            if (!Array.isArray(reviewIds) || reviewIds.length === 0) {
                return;
            }

            const token = getAuthToken();
            if (!token) {
                // User not logged in, no need to load ratings
                return;
            }

            try {
                const idsParam = reviewIds.join(',');
                const response = await fetch(`${apiBaseUrl}/api/Review/batch-ratings?reviewIds=${encodeURIComponent(idsParam)}`, {
                    headers: {
                        "Accept": "application/json",
                        "Authorization": `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    console.warn("Không thể tải trạng thái đánh giá");
                    return;
                }

                const result = await response.json();
                if (!result.isSuccess || !result.data || !result.data.ratings) {
                    console.warn("Dữ liệu batch ratings không hợp lệ");
                    return;
                }

                // Store rating states in reviewRatingState Map
                const ratings = result.data.ratings;
                Object.keys(ratings).forEach(reviewIdStr => {
                    const reviewId = parseInt(reviewIdStr);
                    const ratingInfo = ratings[reviewIdStr];
                    if (ratingInfo && ratingInfo.hasRated && ratingInfo.ratingType !== null) {
                        reviewRatingState.set(reviewId, ratingInfo.ratingType);
                    }
                });

                // Update UI to reflect rating states
                setupReviewActionButtons();
            } catch (error) {
                console.error("Lỗi khi tải batch ratings:", error);
            }
        };

        // Load movie reviews
        const loadMovieReviews = async (page = currentReviewPage) => {
            if (!reviewsContainer) return;

            const targetPage = Number.isFinite(page) && page > 0 ? Math.floor(page) : 1;
            currentReviewPage = targetPage;

            try {
                const token = getAuthToken();
                const requestHeaders = {
                    "Accept": "application/json"
                };
                if (token) {
                    requestHeaders.Authorization = `Bearer ${token}`;
                }

                const response = await fetch(`${apiBaseUrl}/api/Review/movie/${movieId}?page=${targetPage}&pageSize=${REVIEWS_PER_PAGE}`, {
                    headers: requestHeaders
                });
                if (!response.ok) throw new Error("Không thể tải reviews");

                const result = await response.json();
                if (!result.isSuccess || !result.data) throw new Error("Dữ liệu không hợp lệ");

                const payload = result.data;
                const items = Array.isArray(payload.items) ? payload.items : [];
                const totalPagesValue = Number(payload.totalPages);
                const totalPages = Number.isFinite(totalPagesValue) && totalPagesValue > 0 ? totalPagesValue : (items.length > 0 ? 1 : 0);
                const responsePageValue = Number(payload.page);
                const responsePage = Number.isFinite(responsePageValue) && responsePageValue > 0 ? responsePageValue : targetPage;

                if (totalPages > 0 && targetPage > totalPages) {
                    window.location.replace(buildReviewUrl(totalPages));
                    return;
                }

                if (totalPages === 0 && targetPage !== 1) {
                    window.location.replace(buildReviewUrl(1));
                    return;
                }

                currentReviewPage = responsePage;

                if (items.length === 0) {
                    toggleElement(reviewsContainer, false);
                    toggleElement(reviewsEmptyState, true);
                } else {
                    renderReviews(items);
                    toggleElement(reviewsContainer, true);
                    toggleElement(reviewsEmptyState, false);

                    // Load batch ratings for these reviews
                    const reviewIds = items.map(item => item.id).filter(id => Number.isFinite(id));
                    if (reviewIds.length > 0) {
                        await loadBatchRatings(reviewIds);
                    }
                }

                renderPagination(currentReviewPage, totalPages);
            } catch (error) {
                console.error("Lỗi khi tải reviews:", error);
                toggleElement(reviewsContainer, false);
                toggleElement(reviewsEmptyState, true);
                if (paginationContainer) {
                    paginationContainer.classList.add("d-none");
                    paginationContainer.innerHTML = "";
                }
            }
        };

        // Render reviews
        const renderReviews = reviews => {
            if (!reviewsContainer) return;
            reviewsContainer.innerHTML = "";
            reviewRatingState.clear();
            reviews.forEach(review => {
                reviewsContainer.appendChild(createReviewCard(review));
            });
            setupReviewActionButtons();
        };

        // Create review card
        const createReviewCard = review => {
            const col = document.createElement("div");
            col.className = "col-12";

            const userName = review.userName || "Thành viên";
            const initials = safeInitials(userName);
            const supportScore = parseNumberOrNull(review.communicationScore) ?? 0;
            const supportTone = supportScore > 0 ? "positive" : supportScore < 0 ? "negative" : "neutral";
            const supportValue = supportScore > 0 ? `+${supportScore.toFixed(1)}` : supportScore.toFixed(1);
            const createdAt = formatDate(review.createdOnUtc);
            const statusBadge = review.status === 1 ? "Đã duyệt" : review.status === 0 ? "Chờ duyệt" : "Đã xóa";

            // Calculate rating for display
            let displayRating = parseNumberOrNull(review.rating) ?? 0;

            // Format content - handle new {tagId, rating} format
            let displayContent = "";
            let tagListHtml = "";

            if (review.type === REVIEW_TYPE.TAG && review.descriptionTag) {
                if (Array.isArray(review.descriptionTag)) {
                    if (review.descriptionTag.length > 0) {
                        if (typeof review.descriptionTag[0] === 'object' && review.descriptionTag[0].tagId) {
                            // New format: [{tagId, rating}] - render with rating bars
                            const tagItems = review.descriptionTag.map(item => {
                                const rawName = typeof item.tagName === "string" && item.tagName.trim().length > 0
                                    ? item.tagName.trim()
                                    : null;
                                const tag = activeTags.find(t => t.id === item.tagId);
                                const tagName = rawName || (tag ? tag.name : `Tag #${item.tagId}`);
                                const ratingValue = parseNumberOrNull(item.rating) ?? 0;

                                return {
                                    tagId: item.tagId,
                                    tagName: tagName,
                                    rating: ratingValue
                                };
                            });

                            // Calculate average rating for TAG type
                            displayRating = calculateAverageRating(tagItems);

                            // Build tag list HTML with rating bars
                            tagListHtml = `
                                <ul class="tag-rating-list">
                                    ${tagItems.map(item => `
                                        <li class="tag-rating-item">
                                            <div class="tag-rating-item__header">
                                                <span class="tag-rating-item__name">
                                                    <i class="bi bi-tag-fill me-2"></i>${escapeHtml(item.tagName)}
                                                </span>
                                                <span class="tag-rating-item__score">${formatRatingValue(item.rating) ?? "0"}/10</span>
                                            </div>
                                            <div class="tag-rating-item__bar">
                                                ${renderRatingBar(item.rating)}
                                            </div>
                                        </li>
                                    `).join("")}
                                </ul>
                            `;

                            displayContent = tagItems
                                .map(item => `${item.tagName} (${formatRatingValue(item.rating) ?? 0}/10)`)
                                .join(" • ");
                        } else {
                            // Old format fallback
                            displayContent = `[${review.descriptionTag.join(" / ")}]`;
                        }
                    }
                }
            } else {
                displayContent = review.description || "";
            }

            const truncatedText = displayContent.length > 200
                ? `${displayContent.substring(0, 200)}...`
                : displayContent;
            const hasTagMarkup = tagListHtml.trim().length > 0;
            const excerptContent = hasTagMarkup ? tagListHtml : escapeHtml(truncatedText);

            const reviewIdRaw = parseNumberOrNull(review.id);
            const reviewId = reviewIdRaw ?? 0;

            // Check if this review belongs to the current user
            const currentUserId = getCurrentUserId();
            const isOwnReview = currentUserId !== null && review.userId === currentUserId;

            // Disable buttons if this is user's own review
            const disableAttr = isOwnReview ? "disabled" : "";

            col.innerHTML = `
                <article class="community-review">
                    <div class="community-review__support" data-tone="${supportTone}">
                        <span class="community-review__support-value">${supportValue}</span>
                        <span class="community-review__support-label">ủng hộ</span>
                    </div>
                    <div class="community-review__content">
                        <header class="community-review__header">
                            <div class="community-review__author">
                                ${review.userAvatar
                    ? `<img src="${review.userAvatar}" alt="${userName}" class="community-review__avatar" loading="lazy" />`
                    : `<div class="community-review__avatar community-review__avatar--placeholder">
                                        <span class="community-review__initials">${initials}</span>
                                       </div>`}
                                <div>
                                    <span class="community-review__name">${escapeHtml(userName)}</span>
                                    <span class="community-review__badge">${statusBadge}</span>
                                </div>
                            </div>
                            <div class="community-review__meta">
                                <span class="community-review__username">${escapeHtml(userName)}</span>
                                ${displayRating > 0 ? `<span class="community-review__score">${formatRatingValue(displayRating)}/10</span>` : ""}
                            </div>
                        </header>
                        <div class="community-review__excerpt">${excerptContent}</div>
                        <footer class="community-review__footer">
                            <span>${createdAt}</span>
                        </footer>
                    </div>
                    <div class="community-review__actions" data-review-actions="${reviewId}" data-review-user-id="${review.userId}" data-is-own-review="${isOwnReview}">
                        <button type="button" class="community-review__action community-review__action--fair" data-review-action="fair" data-review-id="${reviewId}" data-review-count="fair" aria-pressed="false" ${disableAttr}>
                            <i class="bi bi-hand-thumbs-up"></i>
                            <span class="community-review__action-label">Công tâm</span>
                        </button>
                        <button type="button" class="community-review__action community-review__action--unfair" data-review-action="unfair" data-review-id="${reviewId}" data-review-count="unfair" aria-pressed="false" ${disableAttr}>
                            <i class="bi bi-hand-thumbs-down"></i>
                            <span class="community-review__action-label">Không công tâm</span>
                        </button>
                    </div>
                </article>
            `;

            return col;
        };

        function setupReviewActionButtons() {
            if (!reviewsContainer) return;
            const actionButtons = reviewsContainer.querySelectorAll("[data-review-action]");
            actionButtons.forEach(button => {
                // Remove existing listeners to prevent duplicates
                button.removeEventListener("click", handleReviewActionClick);
                button.addEventListener("click", handleReviewActionClick);

                // Set initial state based on reviewRatingState
                const reviewId = parseNumberOrNull(button.dataset.reviewId);
                if (reviewId !== null && reviewRatingState.has(reviewId)) {
                    const ratingType = reviewRatingState.get(reviewId);
                    reflectReviewActionState(reviewId, ratingType);
                }
            });
        }

        function reflectReviewActionState(reviewId, ratingType) {
            if (!reviewsContainer) return;
            const container = reviewsContainer.querySelector(`[data-review-actions="${reviewId}"]`);
            if (!container) return;

            const buttons = container.querySelectorAll("[data-review-action]");
            buttons.forEach(button => {
                const actionType = button.dataset.reviewAction;
                const isFair = actionType === "fair";
                const isUnfair = actionType === "unfair";
                const isActive = (ratingType === REVIEW_RATING_TYPE.FAIR && isFair)
                    || (ratingType === REVIEW_RATING_TYPE.UNFAIR && isUnfair);

                button.classList.toggle("is-active", isActive);
                button.classList.remove("is-loading");

                // Disable all buttons after voting (user has already rated)
                button.disabled = true;
                button.setAttribute("aria-pressed", isActive ? "true" : "false");
            });
        }

        async function handleReviewActionClick(event) {
            const button = event.currentTarget;
            if (!button || button.disabled) {
                return;
            }

            if (!isLoggedIn()) {
                showLoginPrompt();
                return;
            }

            const reviewId = parseNumberOrNull(button.dataset.reviewId);
            if (reviewId === null) {
                return;
            }

            const action = button.dataset.reviewAction;
            const ratingType = action === "fair" ? REVIEW_RATING_TYPE.FAIR : REVIEW_RATING_TYPE.UNFAIR;

            const token = getAuthToken();
            if (!token) {
                showLoginPrompt();
                return;
            }

            const container = button.closest("[data-review-actions]");
            const siblingButtons = container ? Array.from(container.querySelectorAll("[data-review-action]")) : [button];
            siblingButtons.forEach(btn => {
                btn.classList.add("is-loading");
                btn.disabled = true;
            });

            try {
                const response = await fetch(`${apiBaseUrl}/api/Review/rate`, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "Authorization": `Bearer ${token}`,
                        "Accept": "application/json"
                    },
                    body: JSON.stringify({
                        reviewId,
                        ratingType
                    })
                });

                let result = null;
                try {
                    result = await response.json();
                } catch {
                    /* ignore parse error */
                }

                if (!response.ok || !result?.isSuccess) {
                    const message = result?.message || result?.errorMessage || "Không thể đánh giá review này";
                    throw new Error(message);
                }

                reviewRatingState.set(reviewId, ratingType);
                reflectReviewActionState(reviewId, ratingType);

                await loadMovieReviews(currentReviewPage);
            } catch (error) {
                console.error("Lỗi khi đánh giá review:", error);
                siblingButtons.forEach(btn => {
                    btn.classList.remove("is-loading");
                    btn.disabled = false;
                });
            }
        }

        const renderPagination = (page, totalPages) => {
            if (!paginationContainer) return;

            paginationContainer.innerHTML = "";

            const total = Number.isFinite(totalPages) ? totalPages : 0;
            if (total <= 1) {
                paginationContainer.classList.add("d-none");
                return;
            }

            paginationContainer.classList.remove("d-none");

            const list = document.createElement("div");
            list.className = "review-pagination__list";

            const createLink = (targetPage, label, { disabled = false, active = false } = {}) => {
                if (disabled) {
                    const span = document.createElement("span");
                    span.className = "review-pagination__link is-disabled";
                    span.innerHTML = label;
                    return span;
                }

                const link = document.createElement("a");
                link.className = "review-pagination__link";
                link.href = buildReviewUrl(targetPage);
                link.innerHTML = label;

                if (active) {
                    link.classList.add("is-active");
                    link.setAttribute("aria-current", "page");
                }

                return link;
            };

            const clampedPage = page < 1 ? 1 : page;
            const prevDisabled = clampedPage <= 1;
            const nextDisabled = clampedPage >= total;

            list.appendChild(createLink(clampedPage - 1, '<i class="bi bi-chevron-left"></i>', { disabled: prevDisabled }));

            const windowSize = 5;
            const halfWindow = Math.floor(windowSize / 2);
            let start = Math.max(1, clampedPage - halfWindow);
            let end = Math.min(total, start + windowSize - 1);
            start = Math.max(1, end - windowSize + 1);

            for (let index = start; index <= end; index += 1) {
                list.appendChild(createLink(index, index.toString(), { active: index === clampedPage }));
            }

            list.appendChild(createLink(clampedPage + 1, '<i class="bi bi-chevron-right"></i>', { disabled: nextDisabled }));

            paginationContainer.appendChild(list);
        };

        window.addEventListener("cineReview:userReviewsChanged", event => {
            const detail = event?.detail;
            applyUserReviewState(detail && Array.isArray(detail.reviews) ? detail.reviews : []);
        });

        // Event listeners
        writeReviewButtons.forEach(button => {
            button.addEventListener("click", e => {
                e.preventDefault();
                openReviewSheet();
            });
        });

        closeButtons.forEach(button => {
            button.addEventListener("click", e => {
                e.preventDefault();
                closeReviewSheet();
            });
        });

        modeButtons.forEach(button => {
            button.addEventListener("click", e => {
                e.preventDefault();
                const mode = button.dataset.reviewMode;
                if (mode) switchMode(mode);
            });
        });

        if (freeformTextarea) {
            freeformTextarea.addEventListener("input", updateSubmitButton);
        }

        if (submitButton) {
            submitButton.addEventListener("click", e => {
                e.preventDefault();
                if (!isSubmitting) {
                    showConfirmDialog();
                }
            });
        }

        if (confirmCancel) {
            confirmCancel.addEventListener("click", e => {
                e.preventDefault();
                hideConfirmDialog();
            });
        }

        if (confirmSend) {
            confirmSend.addEventListener("click", e => {
                e.preventDefault();
                submitReview();
            });
        }

        if (startAnotherButton) {
            startAnotherButton.addEventListener("click", e => {
                e.preventDefault();
                hideStatusDialog();
                resetForm();
            });
        }

        if (closeCompleteButton) {
            closeCompleteButton.addEventListener("click", e => {
                e.preventDefault();
                hideStatusDialog();
                closeReviewSheet();
            });
        }

        if (retryButton) {
            retryButton.addEventListener("click", e => {
                e.preventDefault();
                hideStatusDialog();
                if (currentReviewData) {
                    showConfirmDialog();
                }
            });
        }

        if (dismissStatusButton) {
            dismissStatusButton.addEventListener("click", e => {
                e.preventDefault();
                hideStatusDialog();
            });
        }

        // Initialize
        renderSelectedTags();
        renderFreeformRating();
        applyUserReviewState([]);
        if (window.CineReviewButton && typeof window.CineReviewButton.getReviews === "function") {
            applyUserReviewState(window.CineReviewButton.getReviews());
        }

        loadActiveTags();
        loadMovieReviews(currentReviewPage);

        // Expose API
        window.CineReviewSheet = {
            open: openReviewSheet,
            close: closeReviewSheet,
            reload: loadMovieReviews
        };
    });
})();
