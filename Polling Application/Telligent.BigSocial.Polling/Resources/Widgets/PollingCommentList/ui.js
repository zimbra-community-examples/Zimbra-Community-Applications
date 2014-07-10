jQuery(function($){
    if (typeof $.telligent === 'undefined')
            $.telligent = {};

    if (typeof $.telligent.bigsocial === 'undefined')
        $.telligent.bigsocial = {};

    if (typeof $.telligent.bigsocial.widgets === 'undefined')
        $.telligent.bigsocial.widgets = {};

    var _errorHtml = '<div class="message error">{ErrorText}</div>',
        _loadingHtml = '<div class="message loading">{LoadingText}</div>',
        _load = function(context, rebasePager) {
            var data = { w_baseUrl: context.baseUrl };
            if(rebasePager) {
                data[context.pageIndexQueryStringKey] = 1;

                var hashData = $.telligent.evolution.url.hashData();
                hashData[context.pageIndexQueryStringKey] = 1;
                $.telligent.evolution.url.hashData(hashData);
            }
            _setContent(context, _loadingHtml.replace(/{LoadingText}/g, context.loadingText));
            $.telligent.evolution.get({
                url: context.loadCommentsUrl,
                data: data,
                success: function(response) {
                    if(response) {
                        _setContent(context, response);
                    }
                },
                defaultErrorMessage: context.errorText,
                error: function(xhr, desc, ex) {
                    _setContent(context, _errorHtml.replace(/{ErrorText}/g, desc));
                }
            });
        },
        _attachHandlers = function(context)
        {
            $(context.wrapper).bind('evolutionModerateLinkClicked',function(e) {
                var commentId = $(e.target).closest('.content-item').data('commentid');
                _deleteMediaComment(context, commentId, e);
                return false;
            });
        },
        _setContent = function(context, html) {
            context.wrapper.html(html).css("visibility", "visible");
        },
        _deleteMediaComment = function(context, commentId, event) {
            if(confirm(context.deleteVerificationText)) {
                $.telligent.evolution.del({
                    url: $.telligent.evolution.site.getBaseUrl() + 'api.ashx/v2/comments/{CommentId}.json',
                    data: {
                        CommentId: commentId
                    },
                    success: function(response) {
                        var item = $('a[name=comment-'+commentId+']', context.wrapper).closest('li.content-item');
                        var remainingItems = $(event.target).closest('ul').find('li.content-item');;
                        item.slideUp(function() {
                            item.remove();
                            // if there were no more comments, hide the comments list altogether
                            if(context.wrapper.find('li').length === 0) {
                                context.wrapper.css("visibility", "hidden");
                            }
                            _load(context, remainingItems.length === 1);
                        });
                    }
                });
            }
        };

    $.telligent.bigsocial.widgets.pollCommentList = {
        register: function(context) {
            _attachHandlers(context);

            $(document).bind('telligent_poll_commentposted', function(e, message) {
                _load(context);
            });
        }
    };
});