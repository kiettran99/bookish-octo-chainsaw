(() => {
    "use strict";

    /**
     * Quản lý tính năng review cho phim
     * Tích hợp với account.js để lấy token xác thực
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

    document.addEventListener("DOMContentLoaded", () => {
        const root = document.querySelector("[data-review-sheet-root]");
        if (!root) return;

        const movieIdElement = document.querySelector("[data-movie-id]");
        const movieId = movieIdElement ? parseInt(movieIdElement.dataset.movieId) : null;

        if (!movieId) {
            console.warn("Không tìm thấy movie ID");
            return;
        }

        // Get API base URL from configuration
        const apiBaseUrlRaw = (root.dataset.apiBaseUrl || "").trim();
        const apiBaseUrl = apiBaseUrlRaw.replace(/\/+$/, "");

        if (!apiBaseUrl) {
            console.error("Không có cấu hình CineReviewApi:BaseUrl");
            return;
        }

        // Elements - Review Sheet
        const layer = root.querySelector("[data-review-layer]");
        const closeButtons = root.querySelectorAll("[data-review-close]");
        const modeButtons = root.querySelectorAll("[data-review-mode]");
        const categorySelect = root.querySelector("[data-review-category]");
        const tagSelect = root.querySelector("[data-review-tag]");
        const freeformTextarea = root.querySelector("[data-review-freeform]");
        const submitButton = root.querySelector("[data-review-submit]");
        const loader = root.querySelector("[data-review-loader]");

        // Elements - Template sections
        const templateSections = root.querySelectorAll("[data-review-template-section]");
        const freeformSection = root.querySelector("[data-review-freeform-section]");

        // Elements - Confirm dialog
        const confirmDialog = root.querySelector("[data-review-confirm]");
        const confirmCancel = root.querySelector("[data-review-confirm-cancel]");
        const confirmSend = root.querySelector("[data-review-confirm-send]");
        const summaryCategoryEl = root.querySelector("[data-summary-category]");
        const summaryTagEl = root.querySelector("[data-summary-tag]");
        const summaryTemplateList = root.querySelector("[data-review-summary-template]");
        const summaryFreeformText = root.querySelector("[data-review-summary-freeform]");

        // Elements - Status dialog
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

        // Elements - Triggers
        const writeReviewButtons = document.querySelectorAll("[data-write-review]");
        const viewAllReviewsButton = document.querySelector("[data-view-all-reviews]");

        // Elements - Reviews list
        const reviewsContainer = document.querySelector("[data-reviews-container]");
        const reviewsEmptyState = document.querySelector("[data-reviews-empty]");

        // State
        let currentMode = REVIEW_MODE.TEMPLATE;
        let reviewCatalog = {};
        let currentReviewData = null;

        // Parse catalog
        try {
            const catalogJson = root.dataset.reviewCatalog;
            if (catalogJson) {
                reviewCatalog = JSON.parse(catalogJson);
            }
        } catch (error) {
            console.error("Không thể parse review catalog:", error);
        }

        // Get auth token
        const getAuthToken = () => {
            if (window.CineReviewAuth && typeof window.CineReviewAuth.getToken === "function") {
                return window.CineReviewAuth.getToken();
            }
            return null;
        };

        // Check if user is logged in
        const isLoggedIn = () => {
            const token = getAuthToken();
            return token !== null && token !== "";
        };

        // Show login prompt
        const showLoginPrompt = () => {
            const authModal = document.getElementById("authModal");
            if (authModal && window.bootstrap && window.bootstrap.Modal) {
                const modal = new window.bootstrap.Modal(authModal);
                modal.show();
            } else {
                alert("Vui lòng đăng nhập để viết review");
            }
        };

        // Open review sheet
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

        // Close review sheet
        const closeReviewSheet = () => {
            if (layer) {
                layer.setAttribute("hidden", "");
                document.body.style.overflow = "";
            }
            resetForm();
        };

        // Reset form
        const resetForm = () => {
            if (categorySelect) categorySelect.value = "";
            if (tagSelect) {
                tagSelect.value = "";
                tagSelect.disabled = true;
            }
            if (freeformTextarea) freeformTextarea.value = "";

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

        // Update submit button state
        const updateSubmitButton = () => {
            if (!submitButton) return;

            let isValid = false;

            if (currentMode === REVIEW_MODE.TEMPLATE) {
                const hasCategory = categorySelect && categorySelect.value;
                const hasTag = tagSelect && tagSelect.value;
                isValid = hasCategory && hasTag;
            } else {
                const content = freeformTextarea ? freeformTextarea.value.trim() : "";
                isValid = content.length >= 10;
            }

            submitButton.disabled = !isValid;
        };

        // Show confirm dialog
        const showConfirmDialog = () => {
            if (currentMode === REVIEW_MODE.TEMPLATE) {
                const category = categorySelect ? categorySelect.value : "";
                const tag = tagSelect ? tagSelect.value : "";

                if (summaryCategoryEl) summaryCategoryEl.textContent = category;
                if (summaryTagEl) summaryTagEl.textContent = tag;

                toggleElement(summaryTemplateList, true);
                toggleElement(summaryFreeformText, false);

                // API model: CreateReviewRequestModel
                currentReviewData = {
                    tmdbMovieId: movieId,
                    type: 0, // ReviewType.Tag
                    descriptionTag: [category, tag],
                    description: null,
                    rating: 5 // Default rating 1-10 scale
                };
            } else {
                const content = freeformTextarea ? freeformTextarea.value.trim() : "";

                if (summaryFreeformText) {
                    summaryFreeformText.textContent = content;
                }

                toggleElement(summaryTemplateList, false);
                toggleElement(summaryFreeformText, true);

                // API model: CreateReviewRequestModel
                currentReviewData = {
                    tmdbMovieId: movieId,
                    type: 1, // ReviewType.Normal
                    descriptionTag: null,
                    description: content,
                    rating: 5 // Default rating 1-10 scale
                };
            }

            if (confirmDialog) {
                confirmDialog.removeAttribute("hidden");
            }
        };

        // Hide confirm dialog
        const hideConfirmDialog = () => {
            if (confirmDialog) {
                confirmDialog.setAttribute("hidden", "");
            }
        };

        // Submit review
        const submitReview = async () => {
            if (!currentReviewData) return;

            hideConfirmDialog();

            if (layer) {
                toggleElement(loader, true);
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

                const result = await response.json();

                if (!response.ok || !result.isSuccess) {
                    const errorMessage = result.message || result.errorMessage || "Không thể gửi review";
                    showStatusDialog(false, errorMessage);
                    return;
                }

                // Success
                const isTagBased = currentReviewData.type === 0; // ReviewType.Tag
                const message = isTagBased
                    ? "Review của bạn đã được gửi và hiển thị ngay trên trang!"
                    : "Review của bạn đã được gửi. Chúng tôi sẽ duyệt và hiển thị trong thời gian sớm nhất.";

                showStatusDialog(true, message);

                // Reload reviews after a short delay
                setTimeout(() => {
                    loadMovieReviews();
                    // Update button state
                    if (window.CineReviewButton && typeof window.CineReviewButton.checkUserReview === "function") {
                        window.CineReviewButton.checkUserReview();
                    }
                }, 1000);

            } catch (error) {
                console.error("Lỗi khi gửi review:", error);
                showStatusDialog(false, error.message || "Đã xảy ra lỗi khi gửi review. Vui lòng thử lại sau.");
            } finally {
                toggleElement(loader, false);
            }
        };

        // Show status dialog
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

        // Hide status dialog
        const hideStatusDialog = () => {
            if (statusDialog) {
                statusDialog.setAttribute("hidden", "");
            }
        };

        // Load movie reviews
        const loadMovieReviews = async () => {
            if (!reviewsContainer) return;

            try {
                const response = await fetch(`${apiBaseUrl}/api/Review/movie/${movieId}?page=1&pageSize=6`, {
                    method: "GET",
                    headers: {
                        "Accept": "application/json"
                    }
                });

                if (!response.ok) {
                    throw new Error("Không thể tải reviews");
                }

                const result = await response.json();

                if (!result.isSuccess || !result.data) {
                    throw new Error("Dữ liệu không hợp lệ");
                }

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
                const reviewCard = createReviewCard(review);
                reviewsContainer.appendChild(reviewCard);
            });
        };

        // Create review card
        const createReviewCard = review => {
            const col = document.createElement("div");
            col.className = "col-12";

            // Map API response fields
            const hasAvatar = review.userAvatar && review.userAvatar.length > 0;
            const userName = review.userName || "Thành viên";
            const initials = safeInitials(userName);
            const supportScore = review.communicationScore || 0;
            const supportTone = supportScore > 0 ? "positive" : supportScore < 0 ? "negative" : "neutral";
            const supportValue = supportScore > 0 ? `+${supportScore.toFixed(1)}` : supportScore.toFixed(1);
            const createdAt = formatDate(review.createdOnUtc);
            const statusBadge = review.status === 1 ? "Đã duyệt" : review.status === 0 ? "Chờ duyệt" : "Đã xóa";

            // Format content from type
            let displayContent = "";
            if (review.type === 0 && review.descriptionTag && review.descriptionTag.length > 0) {
                // Tag-based review
                displayContent = `[${review.descriptionTag.join(" / ")}]`;
                if (review.description) {
                    displayContent += ` ${review.description}`;
                }
            } else {
                // Normal review
                displayContent = review.description || "";
            }

            // Limit content length for excerpt
            const excerpt = displayContent.length > 200 ? displayContent.substring(0, 200) + "..." : displayContent;

            col.innerHTML = `
                <article class="community-review" data-review-id="${review.id}">
                    <div class="community-review__support" data-tone="${supportTone}">
                        <span class="community-review__support-value">${supportValue}</span>
                        <span class="community-review__support-label">ủng hộ</span>
                    </div>
                    <div class="community-review__content">
                        <header class="community-review__header">
                            <div class="community-review__author">
                                ${hasAvatar
                    ? `<img src="${review.userAvatar}" alt="Avatar ${userName}" class="community-review__avatar" loading="lazy" />`
                    : `<div class="community-review__avatar community-review__avatar--placeholder">
                                        <span class="community-review__initials">${initials}</span>
                                    </div>`
                }
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
                        <button type="button" class="community-review__action community-review__action--fair" data-rate-review="${review.id}" data-is-fair="true">
                            <i class="bi bi-hand-thumbs-up"></i>
                            <span class="community-review__action-label">Công tâm</span>
                        </button>
                        <button type="button" class="community-review__action community-review__action--unfair" data-rate-review="${review.id}" data-is-fair="false">
                            <i class="bi bi-hand-thumbs-down"></i>
                            <span class="community-review__action-label">Không công tâm</span>
                        </button>
                    </div>
                </article>
            `;

            // Attach rate event listeners
            const fairButton = col.querySelector('[data-rate-review][data-is-fair="true"]');
            const unfairButton = col.querySelector('[data-rate-review][data-is-fair="false"]');

            if (fairButton) {
                fairButton.addEventListener("click", () => rateReview(review.id, true));
            }

            if (unfairButton) {
                unfairButton.addEventListener("click", () => rateReview(review.id, false));
            }

            return col;
        };

        // Rate review (fair/unfair)
        const rateReview = async (reviewId, isFair) => {
            if (!isLoggedIn()) {
                showLoginPrompt();
                return;
            }

            const token = getAuthToken();
            if (!token) {
                console.error("Không tìm thấy token xác thực");
                return;
            }

            try {
                const response = await fetch(`${apiBaseUrl}/api/Review/rate`, {
                    method: "POST",
                    headers: {
                        "Content-Type": "application/json",
                        "Authorization": `Bearer ${token}`,
                        "Accept": "application/json"
                    },
                    body: JSON.stringify({
                        reviewId: reviewId,
                        isFair: isFair
                    })
                });

                const result = await response.json();

                if (!response.ok || !result.isSuccess) {
                    console.error("Không thể đánh giá review:", result.message || result.errorMessage);
                    return;
                }

                // Success - reload reviews to update counts
                await loadMovieReviews();

            } catch (error) {
                console.error("Lỗi khi đánh giá review:", error);
            }
        };

        // Get status badge
        const getStatusBadge = status => {
            // ReviewStatus enum: Pending = 0, Released = 1, Deleted = 2
            switch (status) {
                case 1: // Released
                    return '<span class="badge bg-success">Đã duyệt</span>';
                case 0: // Pending
                    return '<span class="badge bg-warning">Chờ duyệt</span>';
                case 2: // Deleted
                    return '<span class="badge bg-danger">Đã xóa</span>';
                default:
                    return "";
            }
        };

        // Escape HTML
        const escapeHtml = text => {
            const div = document.createElement("div");
            div.textContent = text;
            return div.innerHTML;
        };

        // Event listeners - Write review buttons
        writeReviewButtons.forEach(button => {
            button.addEventListener("click", e => {
                e.preventDefault();
                openReviewSheet();
            });
        });

        // Event listeners - Close buttons
        closeButtons.forEach(button => {
            button.addEventListener("click", e => {
                e.preventDefault();
                closeReviewSheet();
            });
        });

        // Event listeners - Mode buttons
        modeButtons.forEach(button => {
            button.addEventListener("click", e => {
                e.preventDefault();
                const mode = button.dataset.reviewMode;
                if (mode) switchMode(mode);
            });
        });

        // Event listeners - Category select
        if (categorySelect) {
            categorySelect.addEventListener("change", () => {
                const category = categorySelect.value;

                if (tagSelect) {
                    tagSelect.innerHTML = '<option value="" disabled hidden selected>Chọn nhận xét</option>';

                    if (category && reviewCatalog[category]) {
                        reviewCatalog[category].forEach(tag => {
                            const option = document.createElement("option");
                            option.value = tag;
                            option.textContent = tag;
                            tagSelect.appendChild(option);
                        });
                        tagSelect.disabled = false;
                    } else {
                        tagSelect.disabled = true;
                    }

                    tagSelect.value = "";
                }

                updateSubmitButton();
            });
        }

        // Event listeners - Tag select
        if (tagSelect) {
            tagSelect.addEventListener("change", updateSubmitButton);
        }

        // Event listeners - Freeform textarea
        if (freeformTextarea) {
            freeformTextarea.addEventListener("input", updateSubmitButton);
        }

        // Event listeners - Submit button
        if (submitButton) {
            submitButton.addEventListener("click", e => {
                e.preventDefault();
                showConfirmDialog();
            });
        }

        // Event listeners - Confirm dialog
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

        // Event listeners - Status dialog
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
            });
        }

        if (dismissStatusButton) {
            dismissStatusButton.addEventListener("click", e => {
                e.preventDefault();
                hideStatusDialog();
            });
        }

        // Event listeners - View all reviews button
        if (viewAllReviewsButton) {
            viewAllReviewsButton.addEventListener("click", e => {
                e.preventDefault();
                openAllReviewsModal();
            });
        }

        // Load reviews on page load
        loadMovieReviews();

        // Expose API
        window.CineReviewSheet = {
            open: openReviewSheet,
            close: closeReviewSheet,
            reload: loadMovieReviews
        };
    });

    // All Reviews Modal
    const openAllReviewsModal = () => {
        const movieIdElement = document.querySelector("[data-movie-id]");
        const movieId = movieIdElement ? parseInt(movieIdElement.dataset.movieId) : null;

        if (!movieId) {
            console.warn("Không tìm thấy movie ID");
            return;
        }

        // Create modal if not exists
        let modal = document.getElementById("allReviewsModal");
        if (!modal) {
            modal = createAllReviewsModal();
            document.body.appendChild(modal);
        }

        // Show modal
        if (window.bootstrap && window.bootstrap.Modal) {
            const bsModal = new window.bootstrap.Modal(modal);
            bsModal.show();

            // Load reviews
            loadAllReviews(movieId, 1);
        }
    };

    const createAllReviewsModal = () => {
        const modal = document.createElement("div");
        modal.id = "allReviewsModal";
        modal.className = "modal fade";
        modal.tabIndex = -1;
        modal.setAttribute("aria-labelledby", "allReviewsModalLabel");
        modal.setAttribute("aria-hidden", "true");

        modal.innerHTML = `
            <div class="modal-dialog modal-dialog-scrollable modal-xl">
                <div class="modal-content bg-dark text-white">
                    <div class="modal-header border-secondary">
                        <h5 class="modal-title" id="allReviewsModalLabel">Tất cả review từ cộng đồng</h5>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
                    </div>
                    <div class="modal-body">
                        <div id="allReviewsContainer" class="row g-4"></div>
                        <div id="allReviewsLoader" class="text-center py-5 d-none">
                            <div class="spinner-border text-info" role="status">
                                <span class="visually-hidden">Loading...</span>
                            </div>
                            <p class="text-secondary mt-3">Đang tải reviews...</p>
                        </div>
                        <div id="allReviewsEmpty" class="text-center py-5 d-none">
                            <i class="bi bi-chat-square-text text-secondary" style="font-size: 3rem;"></i>
                            <p class="text-secondary mt-3">Chưa có review nào cho phim này.</p>
                        </div>
                    </div>
                    <div class="modal-footer border-secondary justify-content-between">
                        <div id="allReviewsPagination"></div>
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Đóng</button>
                    </div>
                </div>
            </div>
        `;

        return modal;
    };

    const loadAllReviews = async (movieId, page = 1, pageSize = 12) => {
        const container = document.getElementById("allReviewsContainer");
        const loader = document.getElementById("allReviewsLoader");
        const empty = document.getElementById("allReviewsEmpty");
        const pagination = document.getElementById("allReviewsPagination");

        if (!container) return;

        // Show loader
        container.classList.add("d-none");
        empty.classList.add("d-none");
        loader.classList.remove("d-none");

        try {
            const root = document.querySelector("[data-review-sheet-root]");
            const apiBaseUrl = root ? root.dataset.apiBaseUrl.replace(/\/+$/, "") : "";

            if (!apiBaseUrl) {
                throw new Error("Không có cấu hình API");
            }

            const response = await fetch(`${apiBaseUrl}/api/Review/movie/${movieId}?page=${page}&pageSize=${pageSize}`, {
                method: "GET",
                headers: {
                    "Accept": "application/json"
                }
            });

            if (!response.ok) {
                throw new Error("Không thể tải reviews");
            }

            const result = await response.json();

            if (!result.isSuccess || !result.data) {
                throw new Error("Dữ liệu không hợp lệ");
            }

            const reviews = result.data;
            const totalPages = result.data.totalPages || 1;

            loader.classList.add("d-none");

            if (reviews.length === 0) {
                empty.classList.remove("d-none");
            } else {
                renderAllReviews(reviews, container);
                renderPagination(movieId, page, totalPages, pagination);
                container.classList.remove("d-none");
            }

        } catch (error) {
            console.error("Lỗi khi tải reviews:", error);
            loader.classList.add("d-none");
            empty.classList.remove("d-none");
        }
    };

    const renderAllReviews = (reviews, container) => {
        container.innerHTML = "";

        reviews.forEach(review => {
            const reviewCard = createReviewCardForModal(review);
            container.appendChild(reviewCard);
        });
    };

    const createReviewCardForModal = review => {
        const col = document.createElement("div");
        col.className = "col-12 col-md-6";

        // Map API response fields
        const hasAvatar = review.userAvatar && review.userAvatar.length > 0;
        const userName = review.userName || "Thành viên";
        const initials = safeInitials(userName);
        const supportScore = review.communicationScore || 0;
        const supportTone = supportScore > 0 ? "positive" : supportScore < 0 ? "negative" : "neutral";
        const supportValue = supportScore > 0 ? `+${supportScore.toFixed(1)}` : supportScore.toFixed(1);
        const createdAt = formatDate(review.createdOnUtc);
        const statusBadge = review.status === 1 ? "Đã duyệt" : review.status === 0 ? "Chờ duyệt" : "Đã xóa";

        // Format content from type
        let displayContent = "";
        if (review.type === 0 && review.descriptionTag && review.descriptionTag.length > 0) {
            // Tag-based review
            displayContent = `[${review.descriptionTag.join(" / ")}]`;
            if (review.description) {
                displayContent += ` ${review.description}`;
            }
        } else {
            // Normal review
            displayContent = review.description || "";
        }

        // Limit content length for excerpt
        const excerpt = displayContent.length > 150 ? displayContent.substring(0, 150) + "..." : displayContent;

        col.innerHTML = `
            <div class="review-card">
                <div class="review-card__header">
                    <div class="review-card__author">
                        <div class="review-card__avatar">
                            ${hasAvatar
                ? `<img src="${review.userAvatar}" alt="${userName}" loading="lazy" />`
                : `<span class="review-card__initials">${initials}</span>`
            }
                        </div>
                        <div>
                            <div class="review-card__name">${escapeHtml(userName)}</div>
                            <span class="review-card__badge">${statusBadge}</span>
                        </div>
                    </div>
                    <div class="review-card__meta">
                        <span class="review-card__date">${createdAt}</span>
                        ${review.rating ? `<span class="review-card__rating-score">${review.rating}/10</span>` : ""}
                    </div>
                </div>
                <div class="review-card__body">
                    <p class="review-card__content">${escapeHtml(excerpt)}</p>
                </div>
                <div class="review-card__footer">
                    <div class="review-card__support" data-support-tone="${supportTone}">
                        <i class="bi bi-hand-thumbs-up"></i>
                        <span>${supportValue}</span>
                    </div>
                    <div class="review-card__stats">
                        <span><i class="bi bi-hand-thumbs-up"></i> ${review.fairVotes || 0}</span>
                        <span><i class="bi bi-hand-thumbs-down"></i> ${review.unfairVotes || 0}</span>
                    </div>
                </div>
            </div>
        `;

        return col;
    };

    const renderPagination = (movieId, currentPage, totalPages, container) => {
        if (!container || totalPages <= 1) {
            container.innerHTML = "";
            return;
        }

        const nav = document.createElement("nav");
        nav.setAttribute("aria-label", "Review pagination");

        const ul = document.createElement("ul");
        ul.className = "pagination pagination-sm mb-0";

        // Previous button
        const prevLi = document.createElement("li");
        prevLi.className = `page-item ${currentPage === 1 ? "disabled" : ""}`;
        prevLi.innerHTML = `<a class="page-link" href="#" data-page="${currentPage - 1}">Trước</a>`;
        ul.appendChild(prevLi);

        // Page numbers
        const maxButtons = 5;
        let startPage = Math.max(1, currentPage - Math.floor(maxButtons / 2));
        let endPage = Math.min(totalPages, startPage + maxButtons - 1);

        if (endPage - startPage < maxButtons - 1) {
            startPage = Math.max(1, endPage - maxButtons + 1);
        }

        for (let i = startPage; i <= endPage; i++) {
            const li = document.createElement("li");
            li.className = `page-item ${i === currentPage ? "active" : ""}`;
            li.innerHTML = `<a class="page-link" href="#" data-page="${i}">${i}</a>`;
            ul.appendChild(li);
        }

        // Next button
        const nextLi = document.createElement("li");
        nextLi.className = `page-item ${currentPage === totalPages ? "disabled" : ""}`;
        nextLi.innerHTML = `<a class="page-link" href="#" data-page="${currentPage + 1}">Sau</a>`;
        ul.appendChild(nextLi);

        nav.appendChild(ul);
        container.innerHTML = "";
        container.appendChild(nav);

        // Add click handlers
        ul.querySelectorAll("a.page-link").forEach(link => {
            link.addEventListener("click", e => {
                e.preventDefault();
                const page = parseInt(link.dataset.page);
                if (page && page !== currentPage) {
                    loadAllReviews(movieId, page);
                }
            });
        });
    };

    const escapeHtml = text => {
        const div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    };
})();
