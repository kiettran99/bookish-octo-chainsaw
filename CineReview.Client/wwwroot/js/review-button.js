(() => {
    "use strict";

    /**
     * Smart Write Review Button
     * Check if user has already reviewed the movie
     */

    document.addEventListener("DOMContentLoaded", () => {
        const movieIdElement = document.querySelector("[data-movie-id]");
        const movieId = movieIdElement ? parseInt(movieIdElement.dataset.movieId) : null;

        if (!movieId) return;

        const root = document.querySelector("[data-review-sheet-root]");
        if (!root) return;

        const apiBaseUrl = (root.dataset.apiBaseUrl || "").trim().replace(/\/+$/, "");
        if (!apiBaseUrl) return;

        const writeReviewButtons = document.querySelectorAll("[data-write-review]");
        let userReviews = [];

        writeReviewButtons.forEach(btn => {
            if (!btn.dataset.defaultClasses) {
                btn.dataset.defaultClasses = btn.className;
            }
            if (!btn.dataset.defaultHtml) {
                btn.dataset.defaultHtml = btn.innerHTML;
            }
        });

        const REVIEW_TYPE = {
            TAG: 0,
            FREEFORM: 1
        };

        const getActiveReviewCount = () => userReviews.filter(review => review?.status !== 2).length;

        const notifyUserReviewChange = () => {
            window.dispatchEvent(new CustomEvent("cineReview:userReviewsChanged", {
                detail: {
                    reviews: userReviews.map(review => ({ ...review }))
                }
            }));
        };

        // Get auth token
        const getAuthToken = () => {
            if (window.CineReviewAuth && typeof window.CineReviewAuth.getToken === "function") {
                return window.CineReviewAuth.getToken();
            }
            return null;
        };

        // Show skeleton loading on buttons
        const showButtonLoading = () => {
            writeReviewButtons.forEach(btn => {
                btn.disabled = true;
                btn.innerHTML = `
                    <span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                    <span>Đang tải...</span>
                `;
            });
        };

        // Update button state
        const updateButtonState = () => {
            const buttons = document.querySelectorAll("[data-write-review]");
            const activeCount = getActiveReviewCount();
            const hasTagReview = userReviews.some(r => r?.type === REVIEW_TYPE.TAG && r?.status !== 2);
            const hasFreeformReview = userReviews.some(r => r?.type === REVIEW_TYPE.FREEFORM && r?.status !== 2);
            const hasBothReviews = hasTagReview && hasFreeformReview;

            buttons.forEach(btn => {
                const defaultClasses = btn.dataset.defaultClasses || btn.className;
                const defaultHtml = btn.dataset.defaultHtml || btn.innerHTML;

                const newBtn = btn.cloneNode(false);
                newBtn.dataset.defaultClasses = defaultClasses;
                newBtn.dataset.defaultHtml = defaultHtml;
                newBtn.className = defaultClasses;
                newBtn.disabled = false;

                if (hasBothReviews) {
                    newBtn.innerHTML = `
                        <i class="bi bi-file-text-fill me-2"></i>
                        <span>Xem review của bạn</span>
                    `;
                    newBtn.title = "Bạn đã hoàn thành cả 2 loại review cho phim này. Click để xem chi tiết.";
                } else if (activeCount > 0) {
                    newBtn.innerHTML = defaultHtml;
                    newBtn.title = `Bạn đã viết ${activeCount} review. Click để viết thêm hoặc xem lại.`;
                } else {
                    newBtn.innerHTML = defaultHtml;
                    newBtn.removeAttribute("title");
                }

                btn.parentNode.replaceChild(newBtn, btn);

                newBtn.addEventListener("click", event => {
                    event.preventDefault();
                    event.stopPropagation();
                    if (window.CineReviewSheet && typeof window.CineReviewSheet.open === "function") {
                        window.CineReviewSheet.open();
                    }
                }, true);
            });
        };

        // Check if user has reviewed
        const checkUserReview = async () => {
            const token = getAuthToken();
            if (!token) {
                userReviews = [];
                updateButtonState();
                notifyUserReviewChange();
                return;
            }

            showButtonLoading();

            try {
                const response = await fetch(`${apiBaseUrl}/api/Review/my-review/movie/${movieId}`, {
                    method: "GET",
                    headers: {
                        "Accept": "application/json",
                        "Authorization": `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    throw new Error("Không thể kiểm tra review");
                }

                const result = await response.json();

                if (result.isSuccess) {
                    const payload = result.data;
                    if (Array.isArray(payload)) {
                        userReviews = payload;
                    } else if (payload) {
                        userReviews = [payload];
                    } else {
                        userReviews = [];
                    }
                } else {
                    userReviews = [];
                }
            } catch (error) {
                // On error, assume no review
                userReviews = [];
            }

            updateButtonState();
            notifyUserReviewChange();
        };

        // Check on page load
        checkUserReview();

        // Re-check after successful review submission
        if (window.CineReviewSheet) {
            const originalReload = window.CineReviewSheet.reload;
            window.CineReviewSheet.reload = function () {
                if (originalReload) originalReload();
                checkUserReview();
            };
        }

        // Expose API
        window.CineReviewButton = {
            checkUserReview,
            getReviews: () => [...userReviews]
        };
    });
})();
