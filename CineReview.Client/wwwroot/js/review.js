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

    // Helper functions
    const toggleElement = (element, shouldShow) => {
        if (!element) return;
        element.classList.toggle("d-none", !shouldShow);
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
        const viewAllReviewsButton = document.querySelector("[data-view-all-reviews]");
        const reviewsContainer = document.querySelector("[data-reviews-container]");
        const reviewsEmptyState = document.querySelector("[data-reviews-empty]");

        // State
        let currentMode = REVIEW_MODE.TEMPLATE;
        let currentReviewData = null;
        let activeTags = [];
        let selectedTagRatings = {}; // {tagId: rating}
        let freeformRating = 5;
        let choicesInstance = null;
        let isSubmitting = false; // Prevent double submission

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
            switchMode(REVIEW_MODE.TEMPLATE);
            updateSubmitButton();
        };

        // Switch mode
        const switchMode = mode => {
            currentMode = mode;

            modeButtons.forEach(btn => {
                const isActive = btn.dataset.reviewMode === mode;
                btn.classList.toggle("is-active", isActive);
                btn.setAttribute("aria-selected", isActive);
            });

            const isTemplate = mode === REVIEW_MODE.TEMPLATE;
            templateSections.forEach(section => toggleElement(section, isTemplate));
            toggleElement(freeformSection, !isTemplate);

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

            submitButton.disabled = !isValid || isSubmitting;
        };

        // Show confirm dialog
        const showConfirmDialog = () => {
            if (!confirmDialog) return;

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

                const avgRating = Math.round(tagRatingsArray.reduce((sum, item) => sum + item.rating, 0) / tagRatingsArray.length);

                currentReviewData = {
                    tmdbMovieId: movieId,
                    type: 0,
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
                    type: 1,
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

        // Load movie reviews
        const loadMovieReviews = async () => {
            if (!reviewsContainer) return;

            try {
                const response = await fetch(`${apiBaseUrl}/api/Review/movie/${movieId}?page=1&pageSize=6`);
                if (!response.ok) throw new Error("Không thể tải reviews");

                const result = await response.json();
                if (!result.isSuccess || !result.data) throw new Error("Dữ liệu không hợp lệ");

                const reviews = result.data;
                if (reviews.length === 0) {
                    toggleElement(reviewsContainer, false);
                    toggleElement(reviewsEmptyState, true);
                } else {
                    renderReviews(reviews);
                    toggleElement(reviewsContainer, true);
                    toggleElement(reviewsEmptyState, false);
                }
            } catch (error) {
                console.error("Lỗi khi tải reviews:", error);
                toggleElement(reviewsContainer, false);
                toggleElement(reviewsEmptyState, true);
            }
        };

        // Render reviews
        const renderReviews = reviews => {
            if (!reviewsContainer) return;
            reviewsContainer.innerHTML = "";
            reviews.forEach(review => {
                reviewsContainer.appendChild(createReviewCard(review));
            });
        };

        // Create review card
        const createReviewCard = review => {
            const col = document.createElement("div");
            col.className = "col-12";

            const userName = review.userName || "Thành viên";
            const initials = safeInitials(userName);
            const supportScore = review.communicationScore || 0;
            const supportTone = supportScore > 0 ? "positive" : supportScore < 0 ? "negative" : "neutral";
            const supportValue = supportScore > 0 ? `+${supportScore.toFixed(1)}` : supportScore.toFixed(1);
            const createdAt = formatDate(review.createdOnUtc);
            const statusBadge = review.status === 1 ? "Đã duyệt" : review.status === 0 ? "Chờ duyệt" : "Đã xóa";

            // Format content - handle new {tagId, rating} format
            let displayContent = "";
            if (review.type === 0 && review.descriptionTag) {
                if (Array.isArray(review.descriptionTag)) {
                    if (review.descriptionTag.length > 0) {
                        if (typeof review.descriptionTag[0] === 'object' && review.descriptionTag[0].tagId) {
                            // New format: [{tagId, rating}] - look up tag names
                            displayContent = review.descriptionTag.map(item => {
                                const tag = activeTags.find(t => t.id === item.tagId);
                                const tagName = tag ? tag.name : `Tag #${item.tagId}`;
                                return `${tagName} (${item.rating}/10)`;
                            }).join(" • ");
                        } else {
                            // Old format fallback
                            displayContent = `[${review.descriptionTag.join(" / ")}]`;
                        }
                    }
                }
            } else {
                displayContent = review.description || "";
            }

            const excerpt = displayContent.length > 200 ? displayContent.substring(0, 200) + "..." : displayContent;

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
                                ${review.rating ? `<span class="community-review__score">${review.rating}/10</span>` : ""}
                            </div>
                        </header>
                        <p class="community-review__excerpt">${escapeHtml(excerpt)}</p>
                        <footer class="community-review__footer">
                            <span>${createdAt}</span>
                        </footer>
                    </div>
                    <div class="community-review__actions">
                        <button type="button" class="community-review__action community-review__action--fair">
                            <i class="bi bi-hand-thumbs-up"></i>
                            <span class="community-review__action-label">Công tâm</span>
                        </button>
                        <button type="button" class="community-review__action community-review__action--unfair">
                            <i class="bi bi-hand-thumbs-down"></i>
                            <span class="community-review__action-label">Không công tâm</span>
                        </button>
                    </div>
                </article>
            `;

            return col;
        };

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
        loadActiveTags();
        renderFreeformRating();
        loadMovieReviews();

        // Expose API
        window.CineReviewSheet = {
            open: openReviewSheet,
            close: closeReviewSheet,
            reload: loadMovieReviews
        };
    });
})();
