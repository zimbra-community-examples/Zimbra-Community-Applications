(function(j, global)
{
	if (typeof j.telligent === 'undefined')
		j.telligent = {};

	if (typeof j.telligent.bigsocial === 'undefined')
		j.telligent.bigsocial = {};

	if (typeof j.telligent.bigsocial.widgets === 'undefined')
		j.telligent.bigsocial.widgets = {};

	var _save = function(context)
	{
		context.successMessage.hide();
		context.moderateMessage.hide();
		context.errorMessage.hide();
		var w = j('#' + context.wrapperId);

		context.save
			.html('<span></span>' + context.publishingText)
			.addClass('disabled');

		j.telligent.evolution.post({
			url: j.telligent.evolution.site.getBaseUrl() + 'api.ashx/v2/comments.json?IncludeFields=Comment.CommentId,Comment.IsApproved',
			data:
			{
                ContentId: context.pollContentId,
                ContentTypeId: context.pollContentTypeId,
				Body: j(context.bodySelector).evolutionComposer('val')
			},
			success: function(response)
			{
				j('.processing', w).css('visibility', 'hidden');

				if(response.Comment.IsApproved)
				{
					context.successMessage.slideDown();
					global.setTimeout(function() { context.successMessage.fadeOut().slideUp(); }, 9999);
					j(document).trigger('telligent_poll_commentposted', '');
				}
				else
				{
					context.moderateMessage.slideDown();
					global.setTimeout(function() { context.moderateMessage.fadeOut().slideUp(); }, 9999);
				}

				j(context.bodySelector).evolutionComposer('val','');
				j(context.bodySelector).change();
				context.save.evolutionValidation('reset');
				context.save.html('<span></span>' + context.publishText).removeClass('disabled');
			},
			error: function(xhr, desc, ex)
			{
				j('.processing', w).css("visibility", "hidden");
				context.save.html('<span></span>' + context.publishText).removeClass('disabled');
				context.errorMessage.html(context.publishErrorText + ' (' + desc + ')').slideDown();
			}
		});
	};

	j.telligent.bigsocial.widgets.pollAddCommentForm =
	{
		register: function(context)
		{
			var body = j(context.bodySelector);
			body.one('focus', function(){
				body.evolutionComposer({
					plugins: ['mentions','hashtags']
				});
			});

			if (document.URL.indexOf('#addcomment') >= 0)
				body.focus();

			j('.internal-link.close-message', j('#' + context.wrapperId)).click(function()
			{
				j(this).blur();
				j(this).closest('.message').fadeOut().slideUp();
				return false;
			});

			context.save.evolutionValidation(
			{
				onValidated: function(isValid, buttonClicked, c)
				{
					if (isValid)
						context.save.removeClass('disabled');
					else
						context.save.addClass('disabled');
				},
				onSuccessfulClick: function(e)
				{
					e.preventDefault();
					j('.processing', context.save.parent()).css("visibility", "visible");
					context.save.addClass('disabled');
					_save(context);
				}
			});

			context.save.evolutionValidation('addField',context.bodySelector,
			{
				required: true,
				maxlength: 1000000,
				messages:
				{
					required: context.bodyRequiredText
				}
			}, '#' + context.wrapperId + ' .field-item.post-body .field-item-validation', null);
		}
	};
})(jQuery, window);