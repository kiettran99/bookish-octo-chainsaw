(() => {
	class MovieReviewSheet {
		constructor(root) {
			this.root = root;
			this.catalog = {};
			try {
				const data = root.dataset.reviewCatalog;
				this.catalog = data ? JSON.parse(data) : {};
			} catch (error) {
				console.error("Không thể đọc cấu hình review", error);
			}

			this.layer = root.querySelector("[data-review-layer]");
			this.confirmDialog = root.querySelector("[data-review-confirm]");
			this.statusDialog = root.querySelector("[data-review-status]");
			this.loader = root.querySelector("[data-review-loader]");
			this.submitButton = root.querySelector("[data-review-submit]");
			this.categorySelect = root.querySelector("[data-review-category]");
			this.tagSelect = root.querySelector("[data-review-tag]");
			this.freeformInput = root.querySelector("[data-review-freeform]");
			this.modeButtons = root.querySelectorAll("[data-review-mode]");
			this.templateSections = root.querySelectorAll("[data-review-template-section]");
			this.freeformSections = root.querySelectorAll("[data-review-freeform-section]");
			this.summaryTemplate = root.querySelector("[data-review-summary-template]");
			this.summaryCategory = root.querySelector("[data-summary-category]");
			this.summaryTag = root.querySelector("[data-summary-tag]");
			this.summaryFreeform = root.querySelector("[data-review-summary-freeform]");
			this.statusIcon = root.querySelector("[data-review-status-icon]");
			this.statusTitle = root.querySelector("[data-review-status-title]");
			this.statusDescription = root.querySelector("[data-review-status-description]");
			this.statusSuccessActions = root.querySelector("[data-review-status-success]");
			this.statusFailureActions = root.querySelector("[data-review-status-failure]");

			this.isSubmitting = false;
			this.mode = "template";
			this.lastSubmitSucceeded = null;

			this.bindEvents();
			this.resetForm();
		}

		bindEvents() {
			this.layer.querySelectorAll("[data-review-close]").forEach(element => {
				element.addEventListener("click", () => this.close());
			});

			this.modeButtons.forEach(button => {
				button.addEventListener("click", () => this.setMode(button.dataset.reviewMode));
			});

			this.categorySelect.addEventListener("change", () => {
				this.updateTagOptions();
				this.updateSubmitState();
			});

			this.tagSelect.addEventListener("change", () => this.updateSubmitState());
			this.freeformInput.addEventListener("input", () => this.updateSubmitState());

			this.submitButton.addEventListener("click", () => this.requestSubmit());

			this.confirmDialog.querySelector("[data-review-confirm-cancel]").addEventListener("click", () => {
				this.confirmDialog.hidden = true;
			});

			this.confirmDialog.querySelector("[data-review-confirm-send]").addEventListener("click", () => this.confirmSubmit());

			this.statusDialog.querySelector("[data-review-start-another]").addEventListener("click", () => this.startAnotherReview());
			this.statusDialog.querySelector("[data-review-close-complete]").addEventListener("click", () => this.close());
			this.statusDialog.querySelector("[data-review-retry]").addEventListener("click", () => this.retrySubmit());
			this.statusDialog.querySelector("[data-review-dismiss-status]").addEventListener("click", () => this.dismissStatus());

			document.addEventListener("keydown", event => {
				if (event.key === "Escape" && !this.layer.hidden) {
					this.close();
				}
			});
		}

		open() {
			this.layer.hidden = false;
			document.body.classList.add("review-sheet-open");
		}

		close() {
			this.layer.hidden = true;
			this.confirmDialog.hidden = true;
			this.statusDialog.hidden = true;
			this.setSubmitting(false);
			this.resetForm();
			document.body.classList.remove("review-sheet-open");
		}

		setMode(mode) {
			if (this.mode === mode) {
				return;
			}

			this.mode = mode;
			this.modeButtons.forEach(button => {
				button.classList.toggle("is-active", button.dataset.reviewMode === mode);
			});

			const showTemplate = mode === "template";
			this.templateSections.forEach(section => section.hidden = !showTemplate);
			this.freeformSections.forEach(section => section.hidden = showTemplate);

			if (showTemplate) {
				this.freeformInput.value = "";
			} else {
				this.categorySelect.value = "";
				this.updateTagOptions();
				this.tagSelect.disabled = true;
				this.tagSelect.selectedIndex = 0;
			}

			this.updateSubmitState();
		}

		updateTagOptions() {
			const selectedCategory = this.categorySelect.value;
			const tags = this.catalog[selectedCategory] || [];
			this.tagSelect.innerHTML = "";

			const placeholder = document.createElement("option");
			placeholder.value = "";
			placeholder.disabled = true;
			placeholder.hidden = true;
			placeholder.selected = true;
			placeholder.textContent = "Chọn nhận xét";
			this.tagSelect.appendChild(placeholder);

			if (!selectedCategory) {
				this.tagSelect.disabled = true;
				return;
			}

			this.tagSelect.disabled = false;
			tags.forEach(tag => {
				const option = document.createElement("option");
				option.value = tag;
				option.textContent = tag;
				this.tagSelect.appendChild(option);
			});
		}

		requestSubmit() {
			if (!this.canSubmit() || this.isSubmitting) {
				return;
			}

			this.renderSummary();
			this.confirmDialog.hidden = false;
		}

		confirmSubmit() {
			this.confirmDialog.hidden = true;
			this.setSubmitting(true);

			// Simulate checkbox removed - use actual API call from review.js
			window.setTimeout(() => {
				const succeeded = true; // Default to success
				this.setSubmitting(false);
				this.showStatus(succeeded);
			}, 1500);
		}

		showStatus(succeeded) {
			this.lastSubmitSucceeded = succeeded;
			this.statusDialog.hidden = false;
			this.statusSuccessActions.hidden = !succeeded;
			this.statusFailureActions.hidden = succeeded;

			const iconBase = succeeded ? "bi-check-circle-fill" : "bi-exclamation-octagon-fill";
			this.statusIcon.className = `review-dialog__status-icon ${iconBase} ${succeeded ? "review-dialog__status-icon--success" : "review-dialog__status-icon--error"}`;
			this.statusTitle.textContent = succeeded ? "Gửi review thành công" : "Không thể gửi review";
			this.statusDescription.textContent = succeeded
				? "Cảm ơn bạn! Review dạng tag đã được hiển thị ngay cho cộng đồng."
				: "Hệ thống đang gặp sự cố giả lập. Bạn có thể thử gửi lại hoặc kiểm tra nội dung trước khi thử lại.";
		}

		startAnotherReview() {
			this.statusDialog.hidden = true;
			this.resetForm();
		}

		retrySubmit() {
			this.statusDialog.hidden = true;
			this.requestSubmit();
		}

		dismissStatus() {
			this.statusDialog.hidden = true;
			this.lastSubmitSucceeded = null;
		}

		setSubmitting(isSubmitting) {
			this.isSubmitting = isSubmitting;
			this.submitButton.disabled = isSubmitting || !this.canSubmit();
			this.loader.hidden = !isSubmitting;
		}

		renderSummary() {
			const templateMode = this.mode === "template";
			this.summaryTemplate.hidden = !templateMode;
			this.summaryFreeform.hidden = templateMode;

			if (templateMode) {
				const category = this.categorySelect.value;
				const tag = this.tagSelect.value;
				this.summaryCategory.textContent = category;
				this.summaryTag.textContent = tag;
				// Note input has been removed, no need to handle it
			} else {
				const text = this.freeformInput.value.trim();
				const preview = text.length > 140 ? `${text.slice(0, 140)}…` : text;
				this.summaryFreeform.textContent = preview;
			}
		}

		canSubmit() {
			if (this.mode === "template") {
				return Boolean(this.categorySelect.value) && Boolean(this.tagSelect.value);
			}

			return this.freeformInput.value.trim().length > 0;
		}

		updateSubmitState() {
			this.submitButton.disabled = !this.canSubmit() || this.isSubmitting;
		}

		resetForm() {
			this.mode = "template";
			this.modeButtons.forEach(button => button.classList.toggle("is-active", button.dataset.reviewMode === this.mode));
			this.templateSections.forEach(section => section.hidden = false);
			this.freeformSections.forEach(section => section.hidden = true);
			this.categorySelect.value = "";
			this.updateTagOptions();
			this.tagSelect.disabled = true;
			this.tagSelect.selectedIndex = 0;
			this.freeformInput.value = "";
			this.lastSubmitSucceeded = null;
			this.updateSubmitState();
		}
	}

	document.addEventListener("DOMContentLoaded", () => {
		const root = document.querySelector("[data-review-sheet-root]");
		if (!root) {
			return;
		}

		const sheet = new MovieReviewSheet(root);
		document.querySelectorAll("[data-review-open]").forEach(trigger => {
			trigger.addEventListener("click", event => {
				event.preventDefault();
				sheet.open();
			});
		});
	});
})();
